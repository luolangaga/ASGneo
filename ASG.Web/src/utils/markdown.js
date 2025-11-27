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

export function renderMarkdown(text) {
  if (!text || typeof text !== 'string') return ''
  return md.render(text)
}
