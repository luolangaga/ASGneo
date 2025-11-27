export function notifySuccess(msg) {
  // 如未接入全局 snackbar，暂用浏览器通知
  console.log('[SUCCESS]', msg)
}

export function notifyError(msg) {
  console.error('[ERROR]', msg)
}