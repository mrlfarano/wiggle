---
version: alpha
name: Wiggle
description: Dark technical design system for a Windows 11 screen recorder — terminal-inspired, developer-native, high-contrast.
colors:
  bg: "#0a0a0a"
  bg-surface: "#121212"
  bg-elevated: "#181818"
  border: "#1e1e1e"
  border-bright: "#2a2a2a"
  text: "#d0d0d0"
  text-bright: "#f0f0f0"
  text-dim: "#555555"
  accent: "#00ff9c"
  accent-muted: "#00b870"
  accent-glow: "rgba(0,255,156,0.08)"
  syntax-string: "#f1fa8c"
  syntax-keyword: "#8be9fd"
  syntax-dim: "#555555"
  error: "#ff5555"
  warning: "#ffb86c"
typography:
  display:
    fontFamily: "'SF Mono','JetBrains Mono','Fira Code',Consolas,monospace"
    fontSize: "2.6rem"
    fontWeight: 800
    lineHeight: 1.1
    letterSpacing: "-0.04em"
  h1:
    fontFamily: "'SF Mono','JetBrains Mono','Fira Code',Consolas,monospace"
    fontSize: "1.6rem"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "-0.02em"
  h2:
    fontFamily: "'SF Mono','JetBrains Mono','Fira Code',Consolas,monospace"
    fontSize: "1.1rem"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "0"
  body:
    fontFamily: "-apple-system,BlinkMacSystemFont,'Segoe UI','Inter',sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.7
  body-sm:
    fontFamily: "-apple-system,BlinkMacSystemFont,'Segoe UI','Inter',sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.6
  mono:
    fontFamily: "'SF Mono','JetBrains Mono','Fira Code',Consolas,monospace"
    fontSize: "0.82rem"
    fontWeight: 400
    lineHeight: 1.9
  mono-sm:
    fontFamily: "'SF Mono','JetBrains Mono','Fira Code',Consolas,monospace"
    fontSize: "0.72rem"
    fontWeight: 400
    lineHeight: 1.5
  stat:
    fontFamily: "'SF Mono','JetBrains Mono','Fira Code',Consolas,monospace"
    fontSize: "1.75rem"
    fontWeight: 800
    lineHeight: 1
    letterSpacing: "-0.03em"
rounded:
  none: "0px"
  sm: "4px"
  md: "6px"
  lg: "8px"
  full: "999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "40px"
  2xl: "64px"
  3xl: "96px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "#000000"
    typography: "{typography.mono}"
    rounded: "{rounded.md}"
    padding: "0.7rem 1.5rem"
  button-primary-hover:
    backgroundColor: "{colors.text-bright}"
    textColor: "#000000"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.text}"
    typography: "{typography.mono}"
    rounded: "{rounded.md}"
    padding: "0.7rem 1.5rem"
  button-ghost-hover:
    textColor: "{colors.accent}"
    backgroundColor: "{colors.accent-glow}"
  terminal:
    backgroundColor: "{colors.bg-surface}"
    textColor: "{colors.text}"
    typography: "{typography.mono}"
    rounded: "{rounded.lg}"
  terminal-bar:
    backgroundColor: "{colors.bg-elevated}"
    rounded: "{rounded.lg}"
  stat-block:
    typography: "{typography.stat}"
    textColor: "{colors.accent}"
---

# Wiggle Design System

## Overview
Wiggle is a native Windows 11 screen recorder for developers and content creators. The design system is dark, monospace-forward, and terminal-native. It should feel like a tool a developer respects — confident, restrained, and fast. The accent color is used sparingly to signal interactivity, never decoratively.

## Colors
The palette is intentionally narrow: near-black backgrounds, gray text, and a single neon green accent. `#0a0a0a` is the canvas; `#121212` and `#181818` provide subtle elevation. The accent `#00ff9c` is reserved for interactive elements, key data points, and the brand wordmark — never for decoration. Text uses a three-tier hierarchy: `#f0f0f0` (primary), `#d0d0d0` (body), `#555555` (meta/captions). This contrast ratio ensures readability without harshness.

## Typography
Two families: a monospace stack for headings, labels, stats, and code; a system sans for body copy. Monospace creates the "developer tool" identity and reads as technical confidence. The type scale is compact (display → body → mono-sm) to keep the page dense and scannable. Letter-spacing tightens on large display sizes to feel modern.

## Layout
Content sits in a max-width 960px container, left-aligned. Sections separate with generous `3xl` (96px) vertical padding — enough whitespace to breathe, not so much that the page feels empty. No multi-column grids on mobile; features flow as stacked lists. The hero uses a 1fr/1fr split on desktop that collapses cleanly.

## Elevation & Depth
Minimal. Borders (`#1e1e1e` and `#2a2a2a`) define boundaries instead of shadows. The terminal component gets a subtle `0 8px 32px rgba(0,0,0,0.5)` shadow to lift it off the page. Everything else is flat — depth is communicated by background-color shifts, not shadow.

## Shapes
Corner radius is restrained: `6px` for buttons and inputs, `8px` for terminal/cards, `999px` for pills/badges only. Sharp corners (`0px`) are never used. The philosophy: slightly rounded = functional/technical, fully rounded = playful/decorative (reserved for badges).

## Components
Buttons use the monospace face at `0.82rem` — small, precise, tool-like. The primary button is accent-on-black (high contrast); ghost buttons are transparent with a border that lights up on hover. The terminal component is the hero's anchor — it should feel like a real terminal window, not a styled div. Stats are large mono numbers with small lowercase labels.

## Do's and Don'ts

**Do:**
- Use monospace for all headings, labels, stats, and code
- Reserve `#00ff9c` for interactive elements and key data — never decorative backgrounds
- Left-align content; avoid centered layouts
- Use generous vertical spacing between sections (96px)
- Let borders define boundaries instead of shadows

**Don't:**
- Don't use gradients of any kind — flat colors only
- Don't use emoji as feature icons
- Don't center the hero — it breaks the technical, left-aligned identity
- Don't use more than two font families (mono + sans)
- Don't add glassmorphism, blur effects, or animated backgrounds
