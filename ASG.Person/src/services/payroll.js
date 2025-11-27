import { apiFetch } from './api'

export async function getPayroll(params = {}) {
  const q = new URLSearchParams()
  Object.entries(params || {}).forEach(([k, v]) => { if (v != null && v !== '') q.set(k, v) })
  const qs = q.toString()
  return apiFetch(`/Payroll${qs ? `?${qs}` : ''}`)
}
export default { getPayroll }
