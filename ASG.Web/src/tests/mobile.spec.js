import { describe, it, expect } from 'vitest'
import { resolveRouteFromNotification } from '../mobile/deeplink'

describe('resolveRouteFromNotification', () => {
  it('maps direct message to messages route with user id', () => {
    const r = resolveRouteFromNotification({ type: 'direct_message', fromUserId: 'u1' })
    expect(r).toBe('/messages/u1')
  })
  it('maps event notification to event page', () => {
    const r = resolveRouteFromNotification({ type: 'event.registration.approved', eventId: 'e1' })
    expect(r).toBe('/events/e1')
  })
  it('falls back to notifications', () => {
    const r = resolveRouteFromNotification({ type: 'other' })
    expect(r).toBe('/notifications')
  })
})
