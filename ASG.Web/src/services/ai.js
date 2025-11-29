import { apiFetch } from './api'

export async function generateTeamLogo({ name, description }) {
  return apiFetch('/Ai/generate-logo', {
    method: 'POST',
    body: JSON.stringify({ name, description }),
  })
}

export async function polishText({ scope, text }) {
  return apiFetch('/Ai/polish-text', {
    method: 'POST',
    body: JSON.stringify({ scope, text }),
  })
}

export async function executeCommand({ command }) {
  return apiFetch('/Ai/command', {
    method: 'POST',
    body: JSON.stringify({ command }),
  })
}

export default { generateTeamLogo, polishText, executeCommand }
