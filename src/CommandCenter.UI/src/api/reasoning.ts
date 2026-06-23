import type {
  CreateReasoningEventCommand,
  CreateReasoningRelationshipCommand,
  CreateReasoningThreadCommand,
  ManualReasoningCaptureCommand,
  ManualReasoningCaptureTemplate,
  ReasoningEvent,
  ReasoningCertificationReport,
  ReasoningGraph,
  ReasoningQuery,
  ReasoningQueryResult,
  ReasoningReconstruction,
  ReasoningMaterializationReviewReport,
  ReasoningMaterializationReviewRequest,
  ReasoningRelationship,
  ReasoningThread,
  ReasoningTrace,
  ReasoningReferenceKind,
} from '../types'
import { invokeCommand } from './tauri'

export function listReasoningEvents(repositoryId: string) {
  return invokeCommand<ReasoningEvent[]>('list_reasoning_events', { repositoryId })
}

export function getReasoningEvent(repositoryId: string, eventId: string) {
  return invokeCommand<ReasoningEvent>('get_reasoning_event', { repositoryId, eventId })
}

export function createReasoningEvent(repositoryId: string, command: CreateReasoningEventCommand) {
  return invokeCommand<ReasoningEvent>('create_reasoning_event', { repositoryId, command })
}

export function listReasoningManualCaptureTemplates(repositoryId: string) {
  return invokeCommand<ManualReasoningCaptureTemplate[]>('list_reasoning_manual_capture_templates', {
    repositoryId,
  })
}

export function captureManualReasoning(
  repositoryId: string,
  command: ManualReasoningCaptureCommand,
) {
  return invokeCommand<ReasoningEvent>('capture_manual_reasoning', { repositoryId, command })
}

export function listReasoningThreads(repositoryId: string) {
  return invokeCommand<ReasoningThread[]>('list_reasoning_threads', { repositoryId })
}

export function getReasoningThread(repositoryId: string, threadId: string) {
  return invokeCommand<ReasoningThread>('get_reasoning_thread', { repositoryId, threadId })
}

export function createReasoningThread(repositoryId: string, command: CreateReasoningThreadCommand) {
  return invokeCommand<ReasoningThread>('create_reasoning_thread', { repositoryId, command })
}

export function appendReasoningThreadEvent(
  repositoryId: string,
  threadId: string,
  eventId: string,
) {
  return invokeCommand<ReasoningThread>('append_reasoning_thread_event', {
    repositoryId,
    threadId,
    eventId,
  })
}

export function listReasoningRelationships(repositoryId: string) {
  return invokeCommand<ReasoningRelationship[]>('list_reasoning_relationships', { repositoryId })
}

export function createReasoningRelationship(
  repositoryId: string,
  command: CreateReasoningRelationshipCommand,
) {
  return invokeCommand<ReasoningRelationship>('create_reasoning_relationship', {
    repositoryId,
    command,
  })
}

export function getReasoningGraph(repositoryId: string) {
  return invokeCommand<ReasoningGraph>('get_reasoning_graph', { repositoryId })
}

export function traceReasoningBackward(
  repositoryId: string,
  kind: ReasoningReferenceKind,
  id: string,
) {
  return invokeCommand<ReasoningTrace>('trace_reasoning_backward', { repositoryId, kind, id })
}

export function traceReasoningForward(
  repositoryId: string,
  kind: ReasoningReferenceKind,
  id: string,
) {
  return invokeCommand<ReasoningTrace>('trace_reasoning_forward', { repositoryId, kind, id })
}

export function queryReasoning(repositoryId: string, query: ReasoningQuery) {
  return invokeCommand<ReasoningQueryResult>('query_reasoning', { repositoryId, query })
}

export function reconstructReasoning(repositoryId: string, query: ReasoningQuery) {
  return invokeCommand<ReasoningReconstruction>('reconstruct_reasoning', { repositoryId, query })
}

export function getReasoningMaterializationReview(repositoryId: string) {
  return invokeCommand<ReasoningMaterializationReviewReport>(
    'get_reasoning_materialization_review',
    { repositoryId },
  )
}

export function runReasoningMaterializationReview(
  repositoryId: string,
  request: ReasoningMaterializationReviewRequest,
) {
  return invokeCommand<ReasoningMaterializationReviewReport>(
    'run_reasoning_materialization_review',
    { repositoryId, request },
  )
}

export function getReasoningCertification(repositoryId: string) {
  return invokeCommand<ReasoningCertificationReport>('get_reasoning_certification', {
    repositoryId,
  })
}

export function runReasoningCertification(repositoryId: string) {
  return invokeCommand<ReasoningCertificationReport>('run_reasoning_certification', {
    repositoryId,
  })
}

export function listReasoningCertificationReports(repositoryId: string) {
  return invokeCommand<ReasoningCertificationReport[]>('list_reasoning_certification_reports', {
    repositoryId,
  })
}
