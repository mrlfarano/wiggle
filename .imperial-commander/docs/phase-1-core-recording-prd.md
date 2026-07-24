# Phase 1: Core Recording - Screen Capture & Cursor Tracking

## Overview
**Duration:** 2 weeks
**Complexity:** High
**Priority:** P0 (Highest)
**Dependencies:** Phase 0 (Foundation)

## Goal
Capture screen and cursor with high precision, enabling all subsequent processing and export functionality.

## Tasks

### 1. Screen Capture Module
60 FPS capture, circular frame buffer, CGDisplayStream/Screen Capture Kit, <10% CPU overhead.

### 2. Cursor Tracking System
CGEvent tap, sub-pixel tracking, timestamp sync with video frames, click/drag detection.

### 3. Basic Recording UI
Start/stop/pause controls, recording indicator, preview window, keyboard shortcuts.

## Deliverable
Functional 60fps screen recording with accurate cursor logging. Raw footage playable with cursor overlay.

## Success Criteria
- 60fps capture <10% CPU
- Cursor synced within 1 frame
- 5-minute recording no memory issues
