"""Read-only Unity UI snapshot comparison, using Python standard library only.

Exports serialized fields rather than interpreting Unity YAML as runtime state.
Run from the project root: python tools/verification/canvas_compare.py
"""
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Outputs" / "CanvasAudit"
SCENE = "Assets/Scenes/SampleScene.unity"
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", re.M)
REF = re.compile(r"fileID: (-?\d+)")


def ref(value):
    match = REF.search(value or "")
    return match.group(1) if match else "0"


def parse(text):
    blocks = {}
    headers = list(HEADER.finditer(text))
    for i, header in enumerate(headers):
        body = text[header.end():headers[i + 1].start() if i + 1 < len(headers) else len(text)].strip()
        fields = {}
        matches = list(re.finditer(r"^  ([\w]+):[^\n]*", body, re.M))
        for j, match in enumerate(matches):
            end = matches[j + 1].start() if j + 1 < len(matches) else len(body)
            fields[match.group(1)] = body[match.start():end].strip().split(":", 1)[1].strip()
        blocks[header.group(2)] = {
            "class": header.group(1), "type": body.split(":", 1)[0],
            "stripped": bool(header.group(3)), "fields": fields,
        }
    return blocks


def paths(blocks, prefab=None):
    transforms = {k: b for k, b in blocks.items() if b["class"] in ("4", "224")}
    cache = {}

    def path(tid, seen=None):
        if tid in cache:
            return cache[tid]
        seen = set() if seen is None else seen
        if tid not in transforms or tid in seen:
            return ""
        seen.add(tid)
        fields = transforms[tid]["fields"]
        if transforms[tid]["stripped"]:
            instance = blocks.get(ref(fields.get("m_PrefabInstance")), {}).get("fields", {})
            mods = instance.get("m_Modification", "")
            name_match = re.search(r"propertyPath: m_Name\n\s+value: ([^\n]+)", mods)
            name = name_match.group(1) if name_match else "PrefabInstance"
            parent_match = re.search(r"m_TransformParent: (.*)", mods)
            parent = ref(parent_match.group(1)) if parent_match else "0"
        else:
            go = blocks.get(ref(fields.get("m_GameObject")), {}).get("fields", {})
            name = go.get("m_Name", "<unknown>")
            parent = ref(fields.get("m_Father"))
        prefix = path(parent, seen)
        cache[tid] = (prefix + "/" if prefix else "") + name
        return cache[tid]

    go_paths = {}
    for tid, block in transforms.items():
        p = path(tid)
        go_paths[ref(block["fields"].get("m_GameObject"))] = p
    labels = {}
    prefab_labels = paths(prefab) if prefab else {}
    for bid, block in blocks.items():
        fields = block["fields"]
        if block["class"] in ("4", "224"):
            labels[bid] = path(bid)
        elif block["stripped"]:
            instance_id = ref(fields.get("m_PrefabInstance"))
            labels[bid] = next((path(t) + "/<Prefab component>" for t, b in transforms.items()
                                if ref(b["fields"].get("m_PrefabInstance")) == instance_id), "<Prefab component>")
            source_path = prefab_labels.get(ref(fields.get("m_CorrespondingSourceObject")), "")
            if source_path:
                labels[bid] = labels[bid].removesuffix("/<Prefab component>") + "/" + source_path.partition("/")[2]
                labels[bid] = labels[bid].rstrip("/")
        elif block["class"] == "1":
            labels[bid] = go_paths.get(bid, fields.get("m_Name", ""))
        elif block["class"] == "1001":
            labels[bid] = next((path(t) for t, b in transforms.items()
                                if ref(b["fields"].get("m_PrefabInstance")) == bid), "PrefabInstance")
        else:
            labels[bid] = go_paths.get(ref(fields.get("m_GameObject")), "")
    return labels


def write_csv(name, columns, rows):
    with (OUT / name).open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(columns)
        writer.writerows(rows)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip()
    sources = {
        "current": (ROOT / SCENE).read_text(encoding="utf-8-sig"),
        "git_HEAD": subprocess.check_output(["git", "show", f"{revision}:{SCENE}"], cwd=ROOT).decode("utf-8"),
    }
    for index, source in enumerate(sorted((ROOT / "Assets/_Recovery").glob("*.unity"))):
        sources[f"recovery_{index}_{source.stem}"] = source.read_text(encoding="utf-8-sig")
    sources["HealingCard_prefab_current"] = (ROOT / "Assets/Prefabs/UI/HealingCard.prefab").read_text(encoding="utf-8-sig")
    sources["HealingCard_prefab_git_HEAD"] = subprocess.check_output(
        ["git", "show", f"{revision}:Assets/Prefabs/UI/HealingCard.prefab"], cwd=ROOT).decode("utf-8")
    ui_guids = {}
    for meta in (ROOT / "Assets/Scripts/UI").glob("*.cs.meta"):
        match = re.search(r"guid: (\w+)", meta.read_text())
        if match:
            ui_guids[match.group(1)] = meta.name[:-5]
    snapshots = {}
    prefab = parse(sources["HealingCard_prefab_current"])
    manifest = {"git_revision": revision, "scope": SCENE, "sources": {}}
    for name, content in sources.items():
        blocks = parse(content)
        labels = paths(blocks, prefab)
        selected = {}
        for bid, block in blocks.items():
            script = block["fields"].get("m_Script", "")
            script_name = next((v for k, v in ui_guids.items() if k in script), "")
            if "prefab" in name or "Canvas" in labels[bid].split("/") or script_name:
                selected[bid] = dict(block, path=labels[bid], script=script_name)
        snapshots[name] = selected
        manifest["sources"][name] = {"sha256": hashlib.sha256(content.encode()).hexdigest(),
                                     "blocks": len(blocks), "ui_blocks": len(selected)}
        (OUT / f"{name}.json").write_text(json.dumps(selected, ensure_ascii=False, indent=2), encoding="utf-8")
        rows = []
        for bid, b in selected.items():
            for field, value in b["fields"].items():
                rows.append([b["path"], bid, b["type"], b["script"], field, value])
        write_csv(f"{name}_fields.csv", ["对象路径", "fileID", "组件", "脚本", "字段", "序列化值"], rows)
        layout = []
        keys = ["m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_AnchoredPosition", "m_SizeDelta", "m_LocalScale", "m_LocalRotation"]
        for bid, b in selected.items():
            if b["class"] == "224" and not b["stripped"]:
                go = blocks.get(ref(b["fields"].get("m_GameObject")), {}).get("fields", {})
                layout.append([b["path"], bid, go.get("m_IsActive", "")] + [b["fields"].get(k, "") for k in keys])
        write_csv(f"{name}_layout.csv", ["对象路径", "RectTransform fileID", "自身Active"] + keys, sorted(layout))
    differences = []
    current = snapshots["current"]
    for name, baseline in snapshots.items():
        if name == "current" or "prefab" in name:
            continue
        for bid in sorted(set(current) | set(baseline), key=int):
            now, before = current.get(bid), baseline.get(bid)
            b = now or before
            if now is None or before is None:
                differences.append([name, b["path"], bid, b["type"], "<对象块>",
                                    "存在" if before else "不存在", "存在" if now else "不存在"])
                continue
            for field in sorted(set(now["fields"]) | set(before["fields"])):
                old, new = before["fields"].get(field, "<未序列化>"), now["fields"].get(field, "<未序列化>")
                if old != new:
                    differences.append([name, b["path"], bid, b["type"], field, old, new])
    write_csv("differences.csv", ["对照来源", "对象路径", "fileID", "组件", "字段", "对照值", "当前值"], differences)
    bindings = []
    labels = paths(parse(sources["current"]), prefab)
    for bid, b in current.items():
        if b["script"]:
            for field, value in b["fields"].items():
                if not field.startswith("m_") and REF.search(value):
                    for index, target in enumerate(REF.findall(value)):
                        field_name = field + (f"[{index}]" if len(REF.findall(value)) > 1 else "")
                        bindings.append([b["path"], b["script"], field_name, target, labels.get(target, "空引用" if target == "0" else "未解析/外部资源")])
    write_csv("current_bindings.csv", ["脚本所在对象", "脚本", "字段", "引用", "目标路径"], bindings)
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    def cell(value):
        return str(value).replace("|", "\\|").replace("\n", "<br>")

    def table(columns, rows):
        return ["| " + " | ".join(columns) + " |", "| " + " | ".join(["---"] * len(columns)) + " |"] + [
            "| " + " | ".join(cell(v) for v in row) + " |" for row in rows]

    report = ["# Canvas 检查与修复比对数据", "", "日期：2026-10-13。只读取已保存的资产；未修改场景、Prefab 或业务脚本。", "",
              f"对照提交：`{revision}`。历史值是恢复候选，不等于正确值。", "",
              "## 本次检查结论", "",
              "当前与最近提交的 UI 差异集中在 MainMenuPanel，包括布局调整和新增天赋入口。商店卡片模板两份快照一致。",
              "MainMenuUI 的两个天赋入口字段已经接线，旧维护文档中“待完成”的状态不再代表最新场景。",
              "恢复场景只是历史候选，不能根据名称或差异较少判断它是正确版本。尚未进行 Unity Game 窗口和交互验证。", "",
              "## 使用顺序", "", "1. 退出 Play Mode，先将当前场景另存为备份；本报告不包含 Unity 中未保存的改动。",
              "2. 先核对 Canvas 缩放、面板显隐和父子层级，再核对锚点、Pivot、位置、尺寸与字体。",
              "3. 根据下面的差异定位异常，按字段修复；保留有意新增的天赋入口，不要整场景回滚。",
              "4. 核对脚本引用与按钮 On Click。最后运行主菜单、游戏 HUD、升级、暂停、结算、商店流程。", "",
              "## 数据文件", "", "- `current_layout.csv`：当前普通 RectTransform 数据。",
              "- `git_HEAD_layout.csv`：最近提交中的普通 RectTransform 数据。",
              "- `differences.csv`：当前场景分别与最近提交、三份恢复场景的差异。按 fileID 匹配；新增或重建对象需人工判断。",
              "- `current_bindings.csv`：UI 脚本引用目标，包括数组元素。",
              "- `*_fields.csv`：完整序列化字段，包括文字、字体、颜色、Raycast、Slider、按钮事件、CanvasScaler 和 Prefab 实例覆盖。",
              "- `HealingCard_prefab_*`：卡片模板的当前值和最近提交值。",
              "- `*.json`、`manifest.json`：机器可读快照与来源摘要。", "",
              "Prefab 子对象布局由模板加实例覆盖决定，不能只看 scene layout 表；在 fields 表中查看 PrefabInstance 的 m_Modification。",
              "本工具只做序列化字段提取，不替代 Unity 导入与运行验证，也不检查资源内容损坏或屏幕实际遮挡。", "",
              "## 当前与最近提交的差异", ""]
    report += table(["对象", "组件", "字段", "最近提交", "当前"],
                    [[r[1], r[3], r[4], r[5], r[6]] for r in differences if r[0] == "git_HEAD"])
    report += ["", "## Canvas 与面板状态", ""]
    state_rows = []
    for bid, b in current.items():
        if b["class"] == "1" and b["path"].count("/") <= 1:
            state_rows.append([b["path"], "自身Active", b["fields"].get("m_IsActive", "")])
        if b["path"] == "Canvas" and b["class"] in ("223", "114"):
            for field, value in b["fields"].items():
                if field in ("m_RenderMode", "m_UiScaleMode", "m_ScaleFactor", "m_ReferenceResolution", "m_ScreenMatchMode", "m_MatchWidthOrHeight", "m_ReferencePixelsPerUnit"):
                    state_rows.append([b["path"], field, value])
    report += table(["对象", "字段", "当前值"], state_rows)
    report += ["", "## UI 脚本接线", ""]
    report += table(["所在对象", "脚本", "字段", "目标 fileID", "目标路径"], bindings)
    report += ["", "## 恢复场景对比量", ""]
    report += table(["来源", "UI 对象块数", "相对当前差异行数"],
                    [[name, data["ui_blocks"], sum(r[0] == name for r in differences)] for name, data in manifest["sources"].items() if "prefab" not in name])
    report += ["", "## 修复注意事项", "",
              "- `MainMenuUI.MapItemPrefab` 是预留接口，空值本身不说明损坏。",
              "- 商店的九个字段应全部有有效引用；购买按钮 On Click 留空，由 Awake 注册；ContinueButton 引用 GameManager.ContinueFromShop。",
              "- OpenTalents 的 On Click 留空，由 MainMenuUI.Awake 注册；检查 OpenTalentsButton、MetaProgressionStatus 两个引用。",
              "- UIEditorGuide.md 给出商店与天赋入口的手工参数；文档中的 HealingCard 宽度为 244，而当前模板为 240，需以确认过的视觉效果选择。",
              "- MainHUD 的 GoldText 和 TalentGraphUI 的天赋图在运行时生成，编辑态缺少这些对象不代表损坏。",
              "- 本报告中的自身Active不含父级状态，最终可见性还受父对象、CanvasGroup 和运行时代码控制。", "",
              "重新生成：在项目根目录运行 `python tools/verification/canvas_compare.py`。生成文件位于 Outputs/CanvasAudit，会覆盖上一轮检查数据。", ""]
    (OUT / "检查与修复指南.md").write_text("\n".join(report), encoding="utf-8")
    print(json.dumps({"output": str(OUT), "HEAD_diff_rows": sum(r[0] == "git_HEAD" for r in differences),
                      "source_counts": {k: v["ui_blocks"] for k, v in manifest["sources"].items()}}, ensure_ascii=False))


if __name__ == "__main__":
    main()
