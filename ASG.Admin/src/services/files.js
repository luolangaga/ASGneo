import { post } from './api'

export async function uploadImage(file, scope) {
  const fd = new FormData()
  fd.append('image', file)
  if (scope) fd.append('scope', scope)
  const res = await post('/files/upload', fd)
  return res
}