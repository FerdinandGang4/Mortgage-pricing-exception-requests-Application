import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

const manager = '11111111-1111-1111-1111-111111111111'
const reviewer = '22222222-2222-2222-2222-222222222222'

function contrast(foreground: string, background: string) {
  function luminance(color: string) {
    const rgb = color.startsWith('#')
      ? color.slice(1).match(/.{2}/g)!.slice(0, 3).map(value => parseInt(value, 16))
      : color.match(/[\d.]+/g)!.slice(0, 3).map(Number)
    const linear = rgb.map(value => {
      const channel = value / 255
      return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
    })
    return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722
  }
  const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a)
  return (values[0] + 0.05) / (values[1] + 0.05)
}

async function expectReadable(page: Page, selector: string, controlBorder = false) {
  const colors = await page.locator(selector).first().evaluate(element => {
    const style = getComputedStyle(element)
    let background = style.backgroundColor
    let parent = element.parentElement
    while (background === 'rgba(0, 0, 0, 0)' && parent) {
      background = getComputedStyle(parent).backgroundColor
      parent = parent.parentElement
    }
    return { foreground: style.color, background, border: style.borderTopColor }
  })
  expect(contrast(colors.foreground, colors.background), selector + ' text contrast').toBeGreaterThanOrEqual(4.5)
  if (controlBorder) expect(contrast(colors.border, colors.background), selector + ' boundary contrast').toBeGreaterThanOrEqual(3)
}

async function seedStatusExamples(page: Page) {
  const ids: string[] = []
  for (const status of ['Pending', 'Approved', 'Declined']) {
    const response = await page.request.post('/api/v1/pricing-exceptions', {
      headers: { 'X-User-Id': manager, 'Idempotency-Key': crypto.randomUUID() },
      data: { mortgageApplicationId: crypto.randomUUID(), discountBps: 50, reason: 'Theme contrast verification.', reviewerId: reviewer },
    })
    expect(response.status()).toBe(201)
    const created = await response.json()
    ids.push(created.id)
    if (status !== 'Pending') {
      const decision = await page.request.post('/api/v1/pricing-exceptions/' + created.id + '/decisions', {
        headers: { 'X-User-Id': reviewer, 'Idempotency-Key': crypto.randomUUID() },
        data: { outcome: status, expectedVersion: 1, reviewedRevisionNumber: 1, reason: 'Theme status example.' },
      })
      expect(decision.status()).toBe(200)
    }
  }
  return ids
}

test('theme toggle is keyboard accessible and persists across reloads', async ({ page }) => {
  await page.goto('/')
  const toggle = page.getByRole('button', { name: 'Toggle dark mode' })
  await expect(toggle).toHaveAttribute('aria-pressed', 'false')
  await toggle.focus()
  await page.keyboard.press('Enter')
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await expect(toggle).toHaveAttribute('aria-pressed', 'true')
  await page.reload()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await expect(toggle).toHaveAttribute('title', 'Switch to light mode')
  await toggle.focus()
  await page.keyboard.press('Space')
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
  await page.reload()
  await expect(toggle).toHaveAttribute('aria-pressed', 'false')
})

for (const theme of ['light', 'dark']) {
  test(theme + ' theme covers panels, status badges, history, forms and readable colors', async ({ page }) => {
    const ids = await seedStatusExamples(page)
    await page.addInitScript(({ mode, requests }) => {
      localStorage.setItem('blueharvest.theme', mode)
      localStorage.setItem('blueharvest.requestIds', JSON.stringify(requests))
    }, { mode: theme, requests: ids })
    await page.goto('/')
    await expect(page.locator('tbody tr')).toHaveCount(3)
    await page.locator('tbody tr').first().getByRole('button', { name: /^View request / }).click()
    await expect(page.locator('body')).toHaveCSS('background-color', theme === 'dark' ? 'rgb(15, 23, 42)' : 'rgb(244, 246, 250)')
    await expect(page.locator('.stat-card').first()).toHaveCSS('background-color', theme === 'dark' ? 'rgb(30, 41, 59)' : 'rgb(255, 255, 255)')
    await expect(page.locator('.detail-panel')).toHaveCSS('background-color', theme === 'dark' ? 'rgb(30, 41, 59)' : 'rgb(255, 255, 255)')
    for (const selector of ['h1', '.stat-top', '.stat-card p', '.panel-heading h2', 'th', 'td', '.detail-fields dd', '.status-badge.pending', '.status-badge.approved', '.status-badge.declined', '.primary', '.secondary']) {
      await expectReadable(page, selector)
    }
    // Header and footer gradients must contrast against every gradient stop.
    const palette = await page.evaluate(() => {
      const style = getComputedStyle(document.documentElement)
      return { text: style.getPropertyValue('--chrome-muted').trim(), backgrounds: ['--chrome-start', '--chrome-mid', '--chrome-end'].map(key => style.getPropertyValue(key).trim()) }
    })
    for (const background of palette.backgrounds) expect(contrast(palette.text, background)).toBeGreaterThanOrEqual(4.5)
    await expect(page.locator('.selected-demo-role')).toHaveCSS('color', theme === 'dark' ? 'rgb(241, 245, 249)' : 'rgb(255, 255, 255)')
    await page.getByRole('button', { name: /^History/ }).click()
    await expect(page.locator('.history-events')).toBeVisible()
    await expectReadable(page, '.history-events p')
    await page.screenshot({ path: 'test-results/theme-' + theme + '-desktop.png', fullPage: true })
    await page.getByRole('button', { name: 'New request', exact: true }).click()
    await expect(page.getByLabel('Assigned reviewer')).toBeEnabled()
    for (const selector of ['.modal-heading h2', '.modal-heading p', '.field input:not([readonly])', '.field select', '.field textarea', '.field > small']) {
      await expectReadable(page, selector, /input|select|textarea/.test(selector))
    }
    await expect(page.locator('.modal')).toHaveCSS('background-color', theme === 'dark' ? 'rgb(30, 41, 59)' : 'rgb(255, 255, 255)')
    await expect(page.locator('html')).toHaveCSS('color-scheme', theme)
  })
}

test('dark mode supports creation approval revision decline and history', async ({ page }) => {
  const pageErrors: string[] = []
  page.on('pageerror', error => pageErrors.push(error.message))
  await page.goto('/')
  await page.getByRole('button', { name: 'Toggle dark mode' }).click()
  await page.getByRole('button', { name: 'New request', exact: true }).click()
  await page.getByLabel('Assigned reviewer').selectOption(reviewer)
  await page.getByLabel('Mortgage application ID').fill(crypto.randomUUID())
  await page.getByLabel('Requested discount').fill('50')
  await page.getByLabel('Supporting reason').fill('Dark mode workflow verification.')
  await page.getByRole('button', { name: 'Submit request', exact: true }).click()
  await expect(page.getByRole('dialog')).not.toBeVisible()
  await page.getByLabel('Select Demo User').selectOption(reviewer)
  await page.getByRole('button', { name: 'Review request', exact: true }).click()
  await expectReadable(page, '.review-summary p')
  await expectReadable(page, '.decision-option.chosen')
  await page.getByRole('button', { name: 'Confirm approval', exact: true }).click()
  await expect(page.getByRole('cell', { name: 'Approved', exact: true })).toBeVisible()
  await page.getByLabel('Select Demo User').selectOption(manager)
  await page.getByRole('button', { name: 'Submit a revision', exact: true }).click()
  await expectReadable(page, '.warning-note')
  await page.getByLabel('Requested discount').fill('75')
  await page.getByLabel('Supporting reason').fill('Revised dark mode offer.')
  await page.getByRole('button', { name: 'Submit revision', exact: true }).click()
  await expect(page.getByRole('dialog')).not.toBeVisible()
  await page.getByLabel('Select Demo User').selectOption(reviewer)
  await page.getByRole('button', { name: 'Review request', exact: true }).click()
  await page.getByLabel('Decline', { exact: false }).check()
  await page.getByLabel('Review note').fill('Revised discount not justified.')
  await page.getByRole('button', { name: 'Confirm decline', exact: true }).click()
  await expect(page.getByRole('cell', { name: 'Declined', exact: true })).toBeVisible()
  await page.getByRole('button', { name: /^History/ }).click()
  await expect(page.locator('.timeline')).toContainText('Revised discount not justified.')
  await expectReadable(page, '.decision-note')
  expect(pageErrors).toEqual([])
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
})

test('theme switching works when theme storage is unavailable', async ({ page }) => {
  await page.addInitScript(() => {
    const getItem = Storage.prototype.getItem
    const setItem = Storage.prototype.setItem
    Storage.prototype.getItem = function (key) {
      if (key === 'blueharvest.theme') throw new Error('Theme storage blocked')
      return getItem.call(this, key)
    }
    Storage.prototype.setItem = function (key, value) {
      if (key === 'blueharvest.theme') throw new Error('Theme storage blocked')
      setItem.call(this, key, value)
    }
  })
  await page.goto('/')
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
  await page.getByRole('button', { name: 'Toggle dark mode' }).click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await page.getByRole('button', { name: 'Toggle dark mode' }).click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
})

test('dark theme toggle and navigation fit a narrow mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 800 })
  await page.goto('/')
  await page.getByRole('button', { name: 'Toggle dark mode' }).click()
  await expect(page.getByLabel('Select Demo User')).toBeVisible()
  await page.getByRole('button', { name: 'Open navigation' }).click()
  await expect(page.locator('.sidebar')).toHaveCSS('background-color', 'rgb(11, 18, 32)')
  await expect(page.getByRole('button', { name: 'Pricing exceptions', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Pricing exceptions', exact: true }).click()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: 'test-results/theme-dark-mobile.png', fullPage: true })
})
