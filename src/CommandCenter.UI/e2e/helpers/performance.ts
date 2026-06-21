export async function measureInteractionLatency(action: () => Promise<void>) {
  const start = performance.now()
  await action()
  return performance.now() - start
}

export async function measureP95InteractionLatency(
  action: (iteration: number) => Promise<number | void>,
  iterations = 20,
) {
  const measurements: number[] = []

  for (let iteration = 0; iteration < iterations; iteration += 1) {
    const start = performance.now()
    const measured = await action(iteration)
    measurements.push(
      typeof measured === 'number'
        ? measured
        : performance.now() - start,
    )
  }

  measurements.sort((left, right) => left - right)
  return measurements[Math.ceil(measurements.length * 0.95) - 1]
}
