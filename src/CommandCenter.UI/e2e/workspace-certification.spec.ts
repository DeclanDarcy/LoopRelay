import { expect, test } from '@playwright/test'
import { measureP95InteractionLatency } from './helpers/performance'
import type { Locator } from '@playwright/test'

async function clickAndMeasureFrame(locator: Locator) {
  return locator.evaluate(
    (element) =>
      new Promise<number>((resolve) => {
        const start = performance.now()
        ;(element as HTMLElement).click()
        requestAnimationFrame(() => resolve(performance.now() - start))
      }),
  )
}

async function mouseDownAndMeasureFrame(locator: Locator) {
  return locator.evaluate(
    (element) =>
      new Promise<number>((resolve) => {
        const start = performance.now()
        element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
        requestAnimationFrame(() => resolve(performance.now() - start))
      }),
  )
}

async function fillAndMeasureFrame(locator: Locator, value: string) {
  return locator.evaluate(
    (element, nextValue) =>
      new Promise<number>((resolve) => {
        const input = element as HTMLInputElement
        const start = performance.now()
        input.focus()
        input.value = nextValue
        input.dispatchEvent(new InputEvent('input', { bubbles: true, data: nextValue }))
        requestAnimationFrame(() => resolve(performance.now() - start))
      }),
    value,
  )
}

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
  const elapsedMs = await clickAndMeasureFrame(targetRepository)

  expect(elapsedMs).toBeLessThan(100)
  await expect(targetRepository).toHaveAttribute('aria-current', 'page')
  await expect(page.getByRole('heading', { name: 'CertificationExecuting' })).toBeVisible()
})

test('does not duplicate workspace loads for repeated selection of the same repository', async ({ page }) => {
  await page.goto('/?mock=workspace-certification')
  await expect(page.getByRole('heading', { name: 'AlphaRepo' })).toBeVisible()
  await page.waitForFunction(
    () => (window.__COMMAND_CENTER_MOCK_STATE__?.commandCalls.get_repository_workspace ?? 0) > 0,
  )

  const workspaceCallsBefore = await page.evaluate(
    () => window.__COMMAND_CENTER_MOCK_STATE__?.commandCalls.get_repository_workspace ?? 0,
  )

  const selectedRepository = page
    .getByLabel('Registered repositories')
    .getByRole('button', { name: /AlphaRepo/ })

  await selectedRepository.click()
  await selectedRepository.click()
  await expect(selectedRepository).toHaveAttribute('aria-current', 'page')

  const workspaceCallsAfter = await page.evaluate(
    () => window.__COMMAND_CENTER_MOCK_STATE__?.commandCalls.get_repository_workspace ?? 0,
  )

  expect(workspaceCallsAfter).toBe(workspaceCallsBefore)
})

test('keeps primary tab switching p95 under 100ms', async ({ page }) => {
  await page.goto('/?mock=workspace-certification')

  const workspaceTab = page.getByRole('button', { name: 'Workspace', exact: true })
  const executionTab = page.getByRole('button', { name: 'Execution', exact: true })

  const p95Ms = await measureP95InteractionLatency(async (iteration) => {
    const target = iteration % 2 === 0 ? executionTab : workspaceTab
    const elapsedMs = await clickAndMeasureFrame(target)
    await expect(target).toHaveAttribute('aria-pressed', 'true')
    return elapsedMs
  })

  expect(p95Ms).toBeLessThan(100)
})

test('keeps command palette interactions p95 under 100ms', async ({ page }) => {
  await page.goto('/?mock=workspace-certification')

  const launcher = page.getByRole('button', { name: /Command Ctrl K/ })
  const search = page.getByLabel('Search navigation')

  const openP95Ms = await measureP95InteractionLatency(async () => {
    const elapsedMs = await clickAndMeasureFrame(launcher)
    await expect(page.getByLabel('Command palette')).toBeVisible()
    await mouseDownAndMeasureFrame(page.locator('.command-palette-backdrop'))
    await expect(page.getByLabel('Command palette')).toBeHidden()
    return elapsedMs
  }, 10)

  await clickAndMeasureFrame(launcher)
  await expect(search).toBeFocused()

  const filterP95Ms = await measureP95InteractionLatency(async (iteration) => {
    const query = iteration % 2 === 0 ? 'continuity' : 'workspace'
    const elapsedMs = await fillAndMeasureFrame(search, query)
    await expect(page.getByRole('button', { name: new RegExp(query, 'i') }).first()).toBeVisible()
    return elapsedMs
  }, 10)

  await fillAndMeasureFrame(search, 'execution')
  const executionTab = page.getByRole('button', { name: 'Execution', exact: true })
  const navigationP95Ms = await measureP95InteractionLatency(async () => {
    const elapsedMs = await clickAndMeasureFrame(
      page.getByLabel('Command palette').getByRole('button', { name: /Execution tab/ }),
    )
    await expect(executionTab).toHaveAttribute('aria-pressed', 'true')
    await clickAndMeasureFrame(launcher)
    await expect(search).toBeFocused()
    await fillAndMeasureFrame(search, 'execution')
    return elapsedMs
  }, 10)

  await mouseDownAndMeasureFrame(page.locator('.command-palette-backdrop'))

  expect(openP95Ms).toBeLessThan(100)
  expect(filterP95Ms).toBeLessThan(100)
  expect(navigationP95Ms).toBeLessThan(100)
})

test('keeps shell navigation available across certified viewports', async ({ page }) => {
  for (const viewport of [
    { width: 1440, height: 900 },
    { width: 1280, height: 800 },
    { width: 390, height: 844 },
  ]) {
    await page.setViewportSize(viewport)
    await page.goto('/?mock=workspace-certification')

    await expect(page.getByLabel('Command Center navigation')).toBeVisible()
    await expect(page.getByRole('button', { name: /Command Ctrl K/ })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Workspace', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Execution', exact: true })).toBeVisible()

    await clickAndMeasureFrame(page.getByRole('button', { name: 'Execution', exact: true }))
    await expect(page.getByLabel('Execution workspace')).toBeVisible()

    await clickAndMeasureFrame(page.getByRole('button', { name: /Command Ctrl K/ }))
    await expect(page.getByLabel('Command palette')).toBeVisible()
    await mouseDownAndMeasureFrame(page.locator('.command-palette-backdrop'))
    await expect(page.getByLabel('Command palette')).toBeHidden()
  }
})
