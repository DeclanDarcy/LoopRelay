import { useCallback, useEffect, useState } from 'react'
import {
  assessDecisionQuality,
  formatError,
  generateDecisionQualityReport,
  generateDecisionQualityTrend,
  getDecisionQualityReport,
  getDecisionQualityTrend,
  listDecisionQualityAssessments,
  listDecisionQualityReports,
  listDecisionQualityTrends,
} from '../api'
import type {
  DecisionQualityAssessment,
  DecisionQualityReport,
  DecisionQualityTrend,
} from '../types'

export function useDecisionQuality(repositoryId: string | null) {
  const [assessments, setAssessments] = useState<DecisionQualityAssessment[]>([])
  const [currentReport, setCurrentReport] = useState<DecisionQualityReport | null>(null)
  const [reports, setReports] = useState<DecisionQualityReport[]>([])
  const [currentTrend, setCurrentTrend] = useState<DecisionQualityTrend | null>(null)
  const [trends, setTrends] = useState<DecisionQualityTrend[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isAssessing, setIsAssessing] = useState(false)
  const [isGeneratingReport, setIsGeneratingReport] = useState(false)
  const [isGeneratingTrend, setIsGeneratingTrend] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!repositoryId) {
      setAssessments([])
      setCurrentReport(null)
      setReports([])
      setCurrentTrend(null)
      setTrends([])
      setIsLoading(false)
      return null
    }

    setIsLoading(true)
    setError(null)
    try {
      const [assessmentHistory, report, reportHistory, trend, trendHistory] = await Promise.all([
        listDecisionQualityAssessments(repositoryId),
        getDecisionQualityReport(repositoryId),
        listDecisionQualityReports(repositoryId),
        getDecisionQualityTrend(repositoryId),
        listDecisionQualityTrends(repositoryId),
      ])
      setAssessments(assessmentHistory)
      setCurrentReport(report)
      setReports(reportHistory)
      setCurrentTrend(trend)
      setTrends(trendHistory)
      return report
    } catch (qualityError) {
      const message = formatError(qualityError)
      setAssessments([])
      setCurrentReport(null)
      setReports([])
      setCurrentTrend(null)
      setTrends([])
      setError(message)
      return null
    } finally {
      setIsLoading(false)
    }
  }, [repositoryId])

  const assessProposal = useCallback(async (proposalId: string | null) => {
    if (!repositoryId || !proposalId) {
      return null
    }

    setIsAssessing(true)
    setError(null)
    try {
      const assessment = await assessDecisionQuality(repositoryId, proposalId)
      await refresh()
      return assessment
    } catch (qualityError) {
      const message = formatError(qualityError)
      setError(message)
      return null
    } finally {
      setIsAssessing(false)
    }
  }, [refresh, repositoryId])

  const generateReport = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsGeneratingReport(true)
    setError(null)
    try {
      const report = await generateDecisionQualityReport(repositoryId)
      await refresh()
      return report
    } catch (qualityError) {
      const message = formatError(qualityError)
      setError(message)
      return null
    } finally {
      setIsGeneratingReport(false)
    }
  }, [refresh, repositoryId])

  const generateTrend = useCallback(async () => {
    if (!repositoryId) {
      return null
    }

    setIsGeneratingTrend(true)
    setError(null)
    try {
      const trend = await generateDecisionQualityTrend(repositoryId)
      await refresh()
      return trend
    } catch (qualityError) {
      const message = formatError(qualityError)
      setError(message)
      return null
    } finally {
      setIsGeneratingTrend(false)
    }
  }, [refresh, repositoryId])

  useEffect(() => {
    if (!repositoryId) {
      const timeoutId = window.setTimeout(() => {
        setAssessments([])
        setCurrentReport(null)
        setReports([])
        setCurrentTrend(null)
        setTrends([])
        setIsLoading(false)
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    const timeoutId = window.setTimeout(() => {
      void refresh().catch(() => undefined)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [refresh, repositoryId])

  return {
    assessments,
    currentReport,
    reports,
    currentTrend,
    trends,
    isLoading,
    isAssessing,
    isGeneratingReport,
    isGeneratingTrend,
    error,
    assessProposal,
    generateReport,
    generateTrend,
    refresh,
    load: refresh,
  }
}
