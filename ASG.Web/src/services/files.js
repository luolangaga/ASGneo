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

export default { uploadImage }