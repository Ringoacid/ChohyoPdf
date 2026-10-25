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
  },
}
