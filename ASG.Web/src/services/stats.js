import { apiFetch } from './api'

export async function getHeroes(campId) {
  const params = new URLSearchParams()
  if (campId) params.set('campId', campId)
  const qs = params.toString()
  return apiFetch(`/Stats/heroes${qs ? `?${qs}` : ''}`)
}

export async function getMeta() {
  return apiFetch('/Stats/meta')
}

export async function getOverview({ season, part, campId, position, top = null, metric = 'use_rate', avgMode } = {}) {
  const params = new URLSearchParams()
  if (season != null) params.set('season', season)
  if (part != null) params.set('part', part)
  if (campId != null) params.set('campId', campId)
  if (position) params.set('position', position)
  if (top != null) params.set('top', top)
  if (metric) params.set('metric', metric)
  if (avgMode) params.set('avgMode', avgMode)
  const qs = params.toString()
  return apiFetch(`/Stats/overview${qs ? `?${qs}` : ''}`)
}

export async function getTrend(heroId, { season, part, position } = {}) {
  const params = new URLSearchParams()
  if (season != null) params.set('season', season)
  if (part != null) params.set('part', part)
  if (position) params.set('position', position)
  const qs = params.toString()
  return apiFetch(`/Stats/trend/${heroId}${qs ? `?${qs}` : ''}`)
}

export async function compareHeroes(heroIds, { metric = 'win_rate', season, part, position } = {}) {
  const params = new URLSearchParams()
  params.set('heroIds', (heroIds || []).join(','))
  if (metric) params.set('metric', metric)
  if (season != null) params.set('season', season)
  if (part != null) params.set('part', part)
  if (position) params.set('position', position)
  const qs = params.toString()
  return apiFetch(`/Stats/compare${qs ? `?${qs}` : ''}`)
}

export async function getCampTrend({ season, part, position } = {}) {
  const params = new URLSearchParams()
  if (season != null) params.set('season', season)
  if (part != null) params.set('part', part)
  if (position) params.set('position', position)
  const qs = params.toString()
  return apiFetch(`/Stats/camp-trend${qs ? `?${qs}` : ''}`)
}
