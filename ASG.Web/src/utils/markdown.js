import MarkdownIt from 'markdown-it'

const md = new MarkdownIt({
  html: false,
  linkify: true,
  breaks: true,
})

export function renderMarkdown(text) {
  if (!text || typeof text !== 'string') return ''
  return md.render(text)
}