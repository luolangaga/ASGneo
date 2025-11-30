import MarkdownIt from 'markdown-it'

const md = new MarkdownIt({
  html: false,
  linkify: true,
  breaks: true,
})

md.renderer.rules.image = function(tokens, idx, options, env, self) {
  const token = tokens[idx]
  const src = token.attrGet('src') || ''
  const titleAttr = token.attrGet('title') || ''
  const alt = token.content || ''
  const title = titleAttr ? ` title="${titleAttr}"` : ''
  return `<span class="md-img"><lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay class="md-img-loader"></lottie-player><img src="${src}" alt="${alt}"${title} loading="lazy" onload="this.previousElementSibling && (this.previousElementSibling.style.display='none')" onerror="this.previousElementSibling && (this.previousElementSibling.style.display='none')"/></span>`
}

function parseBili(url) {
  if (!url || typeof url !== 'string') return null
  const u = url.trim()
  const mBV = u.match(/^(?:https?:\/\/)?(?:www|m)\.?bilibili\.com\/video\/(BV[a-zA-Z0-9]+)/i)
  if (mBV) {
    const p = (u.match(/[?&]p=(\d+)/i) || [])[1]
    return { type: 'video', kind: 'bv', id: mBV[1], page: p ? Number(p) || 1 : 1 }
  }
  const mAV = u.match(/^(?:https?:\/\/)?(?:www|m)\.?bilibili\.com\/video\/av(\d+)/i)
  if (mAV) {
    const p = (u.match(/[?&]p=(\d+)/i) || [])[1]
    return { type: 'video', kind: 'av', id: mAV[1], page: p ? Number(p) || 1 : 1 }
  }
  return null
}

function toBiliEmbed(info) {
  if (!info || info.type !== 'video') return ''
  const page = info.page || 1
  const src = info.kind === 'bv'
    ? `https://player.bilibili.com/player.html?bvid=${encodeURIComponent(info.id)}&page=${page}&autoplay=0`
    : `https://player.bilibili.com/player.html?aid=${encodeURIComponent(info.id)}&page=${page}&autoplay=0`
  return `<div class="md-bili"><iframe src="${src}" allowfullscreen="true" frameborder="0" loading="lazy" referrerpolicy="no-referrer"></iframe></div>`
}

md.core.ruler.after('inline', 'bili-embed', function (state) {
  for (let i = 0; i < state.tokens.length; i++) {
    const tok = state.tokens[i]
    if (tok.type !== 'inline' || !Array.isArray(tok.children)) continue
    const children = tok.children
    for (let j = 0; j < children.length; j++) {
      const t = children[j]
      if (t.type !== 'link_open') continue
      const href = t.attrGet('href') || ''
      const info = parseBili(href)
      if (!info) continue
      let k = j + 1
      while (k < children.length && children[k].type !== 'link_close') k++
      const html = toBiliEmbed(info)
      children[j].type = 'html_inline'
      children[j].tag = ''
      children[j].content = html
      children.splice(j + 1, Math.max(0, k - j))
    }
  }
})

export function renderMarkdown(text) {
  if (!text || typeof text !== 'string') return ''
  return md.render(text)
}
