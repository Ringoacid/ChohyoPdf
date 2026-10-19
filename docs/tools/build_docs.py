"""説明書（docs/manual.md）を生成する。

1. docs/examples と samples を実行して PDF を出力する
2. PDF を pdftoppm で PNG にし、docs/images に保存する
3. docs/manual.template.md の <!-- example: 名前 --> を、サンプルの該当コードに置き換える

必要なもの: .NET SDK、Python 3 と Pillow、pdftoppm（Poppler または TeX Live / xpdf）
使い方: python docs/tools/build_docs.py [--skip-run]
"""
import re
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
EXAMPLES = DOCS / "examples" / "ChohyoPdf.DocExamples"
EXAMPLE_OUT = DOCS / "examples" / "output"
SAMPLE_OUT = ROOT / "samples" / "ChohyoPdf.Samples" / "output"
IMAGES = DOCS / "images"

# 画像の作り方
#   crop  : 1 ページ目を描画し、余白を切り取る
#   page  : 1 ページ目をそのまま描画する（用紙の大きさを決めているサンプル）
#   pages : 各ページの縮小画像を横に並べる（ページ送りのサンプル）
IMAGE_MODES = {
    "crop": [
        "hello", "text-decoration", "text-alignments", "line-spacing", "line-breaking",
        "special-characters", "paragraph-boxes", "named-styles", "table-basic", "table-columns",
        "table-merge", "table-borders", "table-rich-cells", "image-basic", "charts",
        "render-then-draw",
    ],
    "page": [
        "hello-graphics", "image-transparency", "image-fit", "shapes", "colors", "transforms",
        "coordinates", "draw-text", "text-boxes", "shrink-to-fit", "form", "page-setup",
    ],
    "pages": [
        "header-footer", "page-breaks", "keep-with-next", "sections", "watermark", "table-long",
        "table-row-split",
    ],
}
SAMPLE_IMAGES = {"invoice": "pages", "report": "pages", "lowlevel": "pages"}

BORDER = (190, 190, 190)


def run(cmd):
    print("+", " ".join(str(c) for c in cmd))
    subprocess.run(cmd, check=True, cwd=ROOT)


def render(pdf: Path, dpi: int, first: int = 1, last: int = 1):
    with tempfile.TemporaryDirectory() as tmp:
        prefix = Path(tmp) / "p"
        subprocess.run(["pdftoppm", "-r", str(dpi), "-png", "-f", str(first), "-l", str(last), str(pdf), str(prefix)],
                       check=True)
        files = sorted(Path(tmp).glob("p*.png"))
        return [Image.open(f).convert("RGB") for f in files]


def page_count(pdf: Path) -> int:
    out = subprocess.run(["pdfinfo", str(pdf)], check=True, capture_output=True).stdout.decode("latin-1")
    return int(re.search(r"Pages:\s+(\d+)", out).group(1))


def framed(img: Image.Image) -> Image.Image:
    out = img.copy()
    ImageDraw.Draw(out).rectangle([0, 0, out.width - 1, out.height - 1], outline=BORDER)
    return out


def make_crop(pdf: Path) -> Image.Image:
    img = render(pdf, 120)[0]
    bg = Image.new("RGB", img.size, (255, 255, 255))
    box = ImageChops.difference(img, bg).getbbox() or (0, 0, img.width, img.height)
    pad = 18
    box = (max(0, box[0] - pad), max(0, box[1] - pad), min(img.width, box[2] + pad), min(img.height, box[3] + pad))
    return framed(img.crop(box))


def make_page(pdf: Path) -> Image.Image:
    return framed(render(pdf, 120)[0])


def make_pages(pdf: Path, max_pages: int = 4) -> Image.Image:
    n = min(page_count(pdf), max_pages)
    dpi = {1: 80, 2: 56}.get(n, 48)                      # ページが少ないときは大きめに
    pages = [framed(p) for p in render(pdf, dpi, 1, n)]
    gap, label_h = 24, 22
    width = sum(p.width for p in pages) + gap * (len(pages) + 1)
    height = max(p.height for p in pages) + gap + label_h + 8
    sheet = Image.new("RGB", (width, height), (246, 246, 246))
    draw = ImageDraw.Draw(sheet)
    x = gap
    for i, p in enumerate(pages):
        # 影を付けて紙らしく見せる
        draw.rectangle([x + 3, gap + 3, x + p.width + 2, gap + p.height + 2], fill=(215, 215, 215))
        sheet.paste(p, (x, gap))
        label = f"{i + 1}"
        tw = draw.textlength(label)
        draw.text((x + (p.width - tw) / 2, gap + p.height + 6), label, fill=(90, 90, 90))
        x += p.width + gap
    return sheet


def build_images():
    IMAGES.mkdir(parents=True, exist_ok=True)
    makers = {"crop": make_crop, "page": make_page, "pages": make_pages}
    for mode, names in IMAGE_MODES.items():
        for name in names:
            makers[mode](EXAMPLE_OUT / f"{name}.pdf").save(IMAGES / f"{name}.png", optimize=True)
    for name, mode in SAMPLE_IMAGES.items():
        makers[mode](SAMPLE_OUT / f"{name}.pdf").save(IMAGES / f"sample-{name}.png", optimize=True)


def load_examples():
    """「// #example 名前」から「// #end」までを取り出し、インデントを除く。"""
    examples = {}
    for cs in EXAMPLES.glob("*.cs"):
        lines = cs.read_text(encoding="utf-8").splitlines()
        name, body = None, []
        for line in lines:
            m = re.match(r"\s*// #example (\w+)\s*$", line)
            if m:
                name, body = m.group(1), []
            elif name and re.match(r"\s*// #end\s*$", line):
                indent = min(len(l) - len(l.lstrip()) for l in body if l.strip())
                examples[name] = "\n".join(l[indent:] for l in body)
                name = None
            elif name:
                body.append(line)
    return examples


def build_manual():
    examples = load_examples()
    template = (DOCS / "manual.template.md").read_text(encoding="utf-8")
    used = set()

    def replace(m):
        name = m.group(1)
        if name not in examples:
            sys.exit(f"サンプル '{name}' が見つかりません")
        used.add(name)
        return "```csharp\n" + examples[name] + "\n```"

    text = re.sub(r"<!-- example: ([A-Za-z0-9_]+) -->", replace, template)
    header = "<!-- このファイルは docs/tools/build_docs.py で生成しています。編集は manual.template.md に行ってください。 -->\n\n"
    (DOCS / "manual.md").write_text(header + text, encoding="utf-8", newline="\n")

    unused = set(examples) - used
    if unused:
        print("説明書で使っていないサンプル:", ", ".join(sorted(unused)))
    for link in re.findall(r"!\[[^\]]*\]\(([^)]+)\)", text):
        if not (DOCS / link).exists():
            sys.exit(f"画像がありません: {link}")


def main():
    if "--skip-run" not in sys.argv:
        run(["dotnet", "run", "--project", str(EXAMPLES), "--", str(EXAMPLE_OUT)])
        run(["dotnet", "run", "--project", str(ROOT / "samples" / "ChohyoPdf.Samples"), "-f", "net8.0", "--", str(SAMPLE_OUT)])
        build_images()
    build_manual()
    print("生成しました:", DOCS / "manual.md")


if __name__ == "__main__":
    main()
