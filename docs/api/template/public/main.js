// API リファレンス（DocFX）を、サイトのほかのページ（docs/tools/site/page.html）と同じ見た目と動きにする
export default {
  defaultTheme: 'auto',
  start: () => {
    // ほかのページと同じく、テーマは OS の設定に合わせる（テーマを切り替えるボタンは出さない。前に選んだテーマも使わない）
    try {
      localStorage.removeItem('theme')
    } catch {
      // 保存領域が使えなくても、表示には困らない
    }

    // 「ChohyoPdf」はサイトのトップページへ（docfx:rel は API リファレンスの最上位までの相対パス）
    const rel = document.querySelector('meta[name="docfx:rel"]')?.content ?? ''
    const brand = document.querySelector('.navbar-brand')
    if (brand) brand.href = rel + '../index.html'

    // ほかのページと同じく、右端に NuGet と GitHub へのリンクを置く
    const navbar = document.getElementById('navbar')
    if (navbar && !navbar.querySelector('.site-ext')) {
      const ext = document.createElement('div')
      ext.className = 'site-ext'
      for (const [label, href] of [['NuGet', 'https://www.nuget.org/packages/ChohyoPdf'], ['GitHub', 'https://github.com/Ringoacid/ChohyoPdf']]) {
        const a = document.createElement('a')
        a.href = href
        a.textContent = label
        ext.append(a)
      }
      navbar.append(ext)
    }

    keepTocScroll()
  },
}

// 左の目次で項目を選んでも、目次のスクロール位置を変えない。
// DocFX は新しいページを開くと、選んでいる項目が見える所まで目次をスクロールする（下にあった項目は目次の一番下に来る）。
// そこで、目次で項目を押したときのスクロール位置を覚えておき、開いたページで目次を作り終えたら元に戻す。
const tocScrollKey = 'chohyopdf-toc-scroll'

function keepTocScroll() {
  const toc = document.getElementById('toc')
  if (!toc) return
  const scroller = () => toc.querySelector('.overflow-y-auto')

  toc.addEventListener('click', e => {
    const a = e.target.closest('a[href]')
    const list = scroller()
    if (!a || !list) return
    try {
      sessionStorage.setItem(tocScrollKey, JSON.stringify({ href: new URL(a.href, location.href).pathname, top: list.scrollTop }))
    } catch {
      // 保存領域が使えなければ、DocFX のスクロールのまま
    }
  })

  let saved = null
  try {
    saved = JSON.parse(sessionStorage.getItem(tocScrollKey) || 'null')
    sessionStorage.removeItem(tocScrollKey)
  } catch {
    saved = null
  }
  // 目次から開いたページのときだけ戻す（本文のリンクなどで開いたときは、選んでいる項目が見えるようにする）
  if (!saved || saved.href !== location.pathname) return

  // 目次は後から作られ、作った直後に DocFX がスクロールするので、そのあとで戻す
  const restore = () => {
    const list = scroller()
    if (!list || !list.querySelector('li.active')) return false
    requestAnimationFrame(() => { list.scrollTop = saved.top })
    return true
  }
  if (restore()) return
  const observer = new MutationObserver(() => {
    if (restore()) observer.disconnect()
  })
  observer.observe(toc, { childList: true, subtree: true })
  setTimeout(() => observer.disconnect(), 5000)
}
