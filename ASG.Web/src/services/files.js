import { apiFetch } from './api'

// 上传通用图片（用于Markdown等场景）
// 返回 { url: 'http://.../uploads/markdown/xxxx.png' }
export async function uploadImage(file, scope = 'markdown') {
  const form = new FormData()
  form.append('image', file)
  const qs = scope ? `?scope=${encodeURIComponent(scope)}` : ''
  return apiFetch(`/Files/upload-image${qs}`, {
    method: 'POST',
    body: form,
  })
}

// 上传3D模型及其关联资源（多文件）。
// files: File[]，mainName：指定主模型文件名（可选），bundle：指定归档目录（可选）。
// 返回 { bundle, mainUrl, files: [{name,url}] }
export async function uploadModelBundle(files, { mainName = null, bundle = null } = {}) {
  const form = new FormData()
  const arr = Array.from(files || []).filter(Boolean)
  if (!arr.length) throw new Error('请选择至少一个文件')
  for (const f of arr) form.append('files', f, f.name)
  if (mainName) form.append('main', mainName)
  const qs = bundle ? `?bundle=${encodeURIComponent(bundle)}` : ''
  return apiFetch(`/Files/upload-model${qs}`, { method: 'POST', body: form })
}

export default { uploadImage }
