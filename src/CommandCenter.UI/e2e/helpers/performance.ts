export async function measureInteractionLatency(action: () => Promise<void>) {
  const start = performance.now()
  await action()
  return performance.now() - start
}

