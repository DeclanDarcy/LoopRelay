import { expect, test } from '@playwright/test'
import { measureInteractionLatency } from './helpers/performance'

test('loads the workspace certification mock', async ({ page }) => {
  await page.goto('/?mock=workspace-certification')

  await expect(page.getByRole('heading', { name: 'AlphaRepo' })).toBeVisible()
  await expect(page.getByLabel('Repository workspace')).toBeVisible()
})

test('measures primary repository selection latency', async ({ page }) => {
  await page.goto('/?mock=workspace-certification')

  const targetRepository = page
    .getByLabel('Registered repositories')
    .getByRole('button')
    .filter({ hasText: 'CertificationExecuting' })

  await targetRepository.scrollIntoViewIfNeeded()
  const elapsedMs = await measureInteractionLatency(async () => {
    await targetRepository.click()
    await expect(page.getByRole('heading', { name: 'CertificationExecuting' })).toBeVisible()
  })

  expect(elapsedMs).toBeLessThan(1000)
})
