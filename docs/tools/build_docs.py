"""説明書（docs/manual.md）と、GitHub Pages で公開するサイト（_site）を生成する。

1. docs/examples と samples を実行して PDF を出力する
2. PDF を pdftoppm で PNG にし、docs/images に保存する
3. docs/manual.template.md の <!-- example: 名前 --> を、サンプルの該当コードに置き換えて docs/manual.md を作る
4. README.md のコードが、サンプル（QuickStart）と同じかを確かめる
5. サイトを _site に作る。トップページ（index.html）、説明書（manual.html）、デザイナーの説明書（designer.html）、
   API リファレンス（api/。DocFX で src/ChohyoPdf のドキュメントコメントから作る）

必要なもの: .NET SDK、Python 3 と Pillow・markdown-it-py・Pygments、pdftoppm と pdfinfo（Poppler または TeX Live / xpdf）
使い方: python docs/tools/build_docs.py [--skip-run] [--skip-api]
  --skip-run : 1 と 2 を省く（文章だけを直したとき）
  --skip-api : API リファレンスを作らない
"""
import re
import shutil
import subprocess
import sys
import tempfile
from html import escape
from pathlib import Path
from urllib.parse import unquote

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
EXAMPLES = DOCS / "examples" / "ChohyoPdf.DocExamples"
EXAMPLE_OUT = DOCS / "examples" / "output"
SAMPLE_OUT = ROOT / "samples" / "ChohyoPdf.Samples" / "output"
IMAGES = DOCS / "images"
SITE_TEMPLATES = DOCS / "tools" / "site"
SITE = ROOT / "_site"
GITHUB = "https://github.com/Ringoacid/ChohyoPdf"
PAGES = "https://ringoacid.github.io/ChohyoPdf/"

# 画像の作り方
#   crop  : 1 ページ目を描画し、余白を切り取る
#   page  : 1 ページ目をそのまま描画する（用紙の大きさを決めているサンプル）
#   pages : 各ページの縮小画像を横に並べる（ページ送りのサンプル）
IMAGE_MODES = {
    "crop": [
        "hello", "text-decoration", "text-alignments", "line-spacing", "line-breaking",
        "special-characters", "paragraph-boxes", "named-styles", "table-basic", "table-columns",
        "table-merge", "table-borders", "table-rich-cells", "table-auto-width", "image-basic", "image-orientation", "charts",
        "render-then-draw", "superscript-subscript", "tab-stops", "bullet-lists", "numbered-lists", "ruby",
        "quick-start",
    ],
    "page": [
        "hello-graphics", "image-transparency", "image-fit", "shapes", "colors", "transforms",
        "coordinates", "draw-text", "text-boxes", "block-boxes", "shrink-to-fit", "form", "page-setup",
    ],
    "pages": [
        "header-footer", "page-breaks", "keep-with-next", "sections", "watermark", "table-long",
        "table-row-split", "odd-even-headers", "page-number-formats", "internal-links", "table-of-contents",
        "columns", "column-areas", "footnotes", "footnote-numbers",
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


def check_media(text, source):
    """Markdown の画像とリンクのうち、docs の中のファイルを指すものが、あるかを確かめる。"""
    for link in re.findall(r"\]\((images/[^)]+)\)", text):
        if not (DOCS / link).exists():
            sys.exit(f"{source}: 画像か動画がありません: {link}")


def build_manual(examples):
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

    unused = set(examples) - used - {"QuickStart"}   # QuickStart は README とトップページに載せる
    if unused:
        print("説明書で使っていないサンプル:", ", ".join(sorted(unused)))
    check_media(text, "manual.template.md")
    return text


def check_readme(examples):
    """README.md のコードが、実行して画像にしているサンプル（QuickStart）と同じかを確かめる。"""
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    if examples["QuickStart"] not in readme:
        sys.exit("README.md のコードが docs/examples の QuickStart と違います。QuickStart のコードを README.md に写してください")


# ---- サイト ----

# 上部のバーに並べるページ（ファイル名, 表示名）
NAV = [("index.html", "ホーム"), ("manual.html", "説明書"), ("designer.html", "デザイナー"), ("api/", "API リファレンス")]

# Markdown のファイルと、サイトでのページの対応
PAGE_OF = {"manual.md": "manual.html", "designer.md": "designer.html"}

COPY_ICON = ('<svg viewBox="0 0 16 16" aria-hidden="true"><path fill="currentColor" d="M0 6.75C0 5.784.784 5 1.75 5h1.5a.75.75 0 0 1 0 '
             '1.5h-1.5a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-1.5a.75.75 0 0 1 1.5 0v1.5A1.75 1.75 0 0 1 '
             '9.25 16h-7.5A1.75 1.75 0 0 1 0 14.25Z"/><path fill="currentColor" d="M5 1.75C5 .784 5.784 0 6.75 0h7.5C15.216 0 16 .784 16 '
             '1.75v7.5A1.75 1.75 0 0 1 14.25 11h-7.5A1.75 1.75 0 0 1 5 9.25Zm1.75-.25a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 '
             '0 0 0 .25-.25v-7.5a.25.25 0 0 0-.25-.25Z"/></svg>')


def github_slug(text):
    """GitHub が見出しに付ける id と同じ規則（小文字にし、記号を除き、空白を - にする）で作る。"""
    return re.sub(r"[^\w\- ]", "", text.lower()).replace(" ", "-")


def site_href(href):
    """docs の Markdown からのリンクを、サイトでのリンクにする。サイトにないファイルは GitHub 上の URL にする。"""
    if href.startswith("#"):
        return href
    if href.startswith(PAGES):
        return href[len(PAGES):] or "index.html"
    if re.match(r"[a-z]+:", href):
        return href
    path, _, fragment = unquote(href).partition("#")
    if path in PAGE_OF:
        return PAGE_OF[path] + ("#" + fragment if fragment else "")
    if path.startswith("images/"):
        return href
    target = (DOCS / path).resolve()
    kind = "tree" if target.is_dir() else "blob"
    url = f"{GITHUB}/{kind}/main/{target.relative_to(ROOT).as_posix()}"
    return url + ("#" + fragment if fragment else "")


class Site:
    def __init__(self):
        try:
            from markdown_it import MarkdownIt
            from pygments import highlight
            from pygments.formatters import HtmlFormatter
            from pygments.lexers import get_lexer_by_name
        except ImportError:
            sys.exit("サイトの生成には markdown-it-py と Pygments が必要です: pip install markdown-it-py pygments")
        self.md = MarkdownIt("commonmark").enable(["table", "strikethrough"])
        self.md.add_render_rule("fence", lambda s, tokens, idx, options, env: self.code_block(tokens[idx].content, tokens[idx].info))
        self.highlight, self.get_lexer, formatter_class = highlight, get_lexer_by_name, HtmlFormatter
        self.formatter = HtmlFormatter(nowrap=True)
        self.css = (f"@media (prefers-color-scheme: light) {{\n{self.highlight_css(formatter_class, 'vs')}\n}}\n"
                    f"@media (prefers-color-scheme: dark) {{\n{self.highlight_css(formatter_class, 'github-dark')}\n}}")
        self.template = (SITE_TEMPLATES / "page.html").read_text(encoding="utf-8")
        self.ids = {}     # ページごとの見出しの id（ページをまたぐリンクの確認に使う）
        self.links = []   # (ページ, リンク先)

    @staticmethod
    def highlight_css(formatter_class, style):
        """Pygments のスタイルのうち、トークンの色の指定だけを取り出す（背景や余白は雛形で決める）。"""
        prefix = ".code pre"
        css = formatter_class(style=style).get_style_defs(prefix)
        return "\n".join(line for line in css.splitlines() if line.startswith(prefix + " ."))

    def code_block(self, code, info):
        """色付けしたコードを、言語名とコピーボタンの付いた枠に入れる。"""
        lang = info.strip().split(" ")[0]
        code = code.rstrip("\n")
        body = self.highlight(code, self.get_lexer(lang), self.formatter).rstrip("\n") if lang else escape(code)
        label = {"csharp": "C#", "xml": "XML"}.get(lang, lang)
        return (f'<div class="code"><div class="code-bar"><span>{label}</span>'
                f'<button type="button" class="copy">{COPY_ICON}<span>コピー</span></button></div>'
                f'<pre><code>{body}</code></pre></div>\n')

    def render_doc(self, name, text):
        """Markdown の文書を HTML にする。返り値は (表題, 目次の HTML, 本文の HTML)。"""
        # 本文の「目次」の節は、左側の目次と重なるので入れない
        text = re.sub(r"^## 目次\n.*?^---\n", "", text, count=1, flags=re.M | re.S)
        tokens = self.md.parse(text)

        # 見出しに GitHub と同じ id を付け、h2 と h3 から目次を作る
        title, ids, toc = "", set(), []
        for i, token in enumerate(tokens):
            if token.type != "heading_open":
                continue
            inline = tokens[i + 1]
            plain = "".join(c.content for c in inline.children if c.type in ("text", "code_inline"))
            if token.tag == "h1":
                title = plain
                continue
            base = slug = github_slug(plain)
            n = 1
            while slug in ids:
                slug, n = f"{base}-{n}", n + 1
            ids.add(slug)
            token.attrSet("id", slug)
            if token.tag in ("h2", "h3"):
                toc.append((token.tag, slug, self.md.renderer.renderInline(inline.children, self.md.options, {})))
        self.ids[name] = ids

        nav = ['<ul class="toc">']
        for i, (tag, slug, label) in enumerate(toc):
            last_in_chapter = i + 1 == len(toc) or toc[i + 1][0] == "h2"
            if tag == "h2":
                nav.append(f'<li class="chapter"><a href="#{slug}">{label}</a>' + ("</li>" if last_in_chapter else "<ul>"))
            else:
                nav.append(f'<li><a href="#{slug}">{label}</a></li>' + ("</ul></li>" if last_in_chapter else ""))
        nav.append("</ul>")

        html = self.md.renderer.render(tokens, self.md.options, {})
        html = re.sub(r'<(h[2-4]) id="([^"]+)">(.*?)</\1>',
                      r'<\1 id="\2">\3<a class="anchor" href="#\2" aria-label="この見出しへのリンク">#</a></\1>', html)
        html = re.sub(r'href="([^"]+)"', lambda m: f'href="{site_href(m.group(1))}"', html)
        html = html.replace("<table>", '<div class="table-wrap"><table>').replace("</table>", "</table></div>")
        # ポスター画像から MP4 へのリンク（GitHub では画像を押すと動画を開く）は、ページの中で再生する動画にする
        html = re.sub(r'<p><a href="([^"]+\.mp4)"><img src="([^"]+)" alt="([^"]*)" /></a></p>',
                      r'<figure class="video"><video controls preload="none" poster="\2" aria-label="\3">'
                      r'<source src="\1" type="video/mp4"></video></figure>', html)
        html = re.sub(r'<p><img src="([^"]+)" alt="([^"]*)" /></p>',
                      r'<a class="figure" href="\1"><img src="\1" alt="\2" loading="lazy"></a>', html)
        return title, "\n".join(nav), html

    def write(self, file, page_title, description, content, footer, sidebar=""):
        nav = "".join(f'<a href="{href}"' + (' aria-current="page"' if href == file else "") + f">{label}</a>"
                      for href, label in NAV)
        nav = f'<nav class="site-nav" aria-label="サイト">{nav}</nav>'
        if sidebar:
            sidebar = (f'  <nav class="sidebar" id="toc" aria-label="目次">\n    {nav}\n    {sidebar}\n  </nav>\n'
                       '  <div class="backdrop"></div>')
        values = {
            "page_title": escape(page_title), "description": escape(description), "head_extra": "",
            "highlight_css": self.css, "body_class": "has-toc" if sidebar else "home", "brand": "ChohyoPdf",
            "nav": nav, "sidebar": sidebar, "content": content, "footer": footer,
        }
        page = self.template
        for key, value in values.items():
            page = page.replace("{{" + key + "}}", value)
        for href in re.findall(r'href="([^"]+)"', content):
            self.links.append((file, href))
        (SITE / file).write_text(page, encoding="utf-8", newline="\n")

    def doc_page(self, file, source, text, description):
        title, toc, html = self.render_doc(file, text)
        footer = f'このページは <a href="{GITHUB}/blob/main/docs/{source}">docs/{source}</a> と同じ内容です。'
        self.write(file, title, description, html, footer, sidebar=toc)

    def home_page(self, examples):
        content = (SITE_TEMPLATES / "home.html").read_text(encoding="utf-8")
        content = content.replace("{{install}}", self.code_block("dotnet add package ChohyoPdf", ""))
        content = content.replace("{{quickstart}}", self.code_block("using ChohyoPdf;\n\n" + examples["QuickStart"], "csharp"))
        self.write("index.html", "ChohyoPdf - 日本語の帳票・文書を作る C# の PDF 生成ライブラリ",
                   "日本語の帳票・文書を作るための、C# 製 PDF 生成ライブラリ（MIT ライセンス）", content,
                   f'ChohyoPdf は MIT ライセンスで公開しています。<a href="{GITHUB}">GitHub</a> ・ '
                   '<a href="https://www.nuget.org/packages/ChohyoPdf">NuGet</a>')

    def check_links(self):
        """ページの中とページの間のリンク先（見出し・画像・動画）があるかを確かめる。"""
        for page, href in self.links:
            if re.match(r"[a-z]+:", href):
                continue
            path, _, fragment = unquote(href).partition("#")
            target = path or page
            if target.startswith("api/"):
                continue
            if not (SITE / target).exists():
                sys.exit(f"{page}: リンク先がありません: {href}")
            if fragment and target in self.ids and fragment not in self.ids[target]:
                sys.exit(f"{page}: リンク先の見出しがありません: {href}")


def build_site(manual_text, examples, with_api):
    if SITE.exists():
        shutil.rmtree(SITE)
    SITE.mkdir()
    if with_api:
        run(["dotnet", "tool", "restore"])
        run(["dotnet", "tool", "run", "docfx", str(DOCS / "api" / "docfx.json")])
    shutil.copytree(IMAGES, SITE / "images")

    site = Site()
    designer_text = (DOCS / "designer.md").read_text(encoding="utf-8")
    check_media(designer_text, "designer.md")
    site.doc_page("manual.html", "manual.md", manual_text, "ChohyoPdf の機能ごとの使い方を、サンプルコードと出力結果の画像で説明します。")
    site.doc_page("designer.html", "designer.md", designer_text, "画面の上で部品を並べて帳票をデザインし、PDF と C# のコードを出力する ChohyoPdf Designer の説明書です。")
    site.home_page(examples)
    site.check_links()


def main():
    if "--skip-run" not in sys.argv:
        run(["dotnet", "run", "--project", str(EXAMPLES), "--", str(EXAMPLE_OUT)])
        run(["dotnet", "run", "--project", str(ROOT / "samples" / "ChohyoPdf.Samples"), "-f", "net8.0", "--", str(SAMPLE_OUT)])
        build_images()
    examples = load_examples()
    manual_text = build_manual(examples)
    check_readme(examples)
    build_site(manual_text, examples, "--skip-api" not in sys.argv)
    print("生成しました:", DOCS / "manual.md", SITE)


if __name__ == "__main__":
    main()
