// ─── Color tokens ──────────────────────────────────────────────────────────────
export function useColors(isDark: boolean) {
  return {
    pageBg:      isDark ? '#0f1117' : '#f9fafb',
    navBg:       isDark ? '#13151c' : '#ffffff',
    navBorder:   isDark ? '#1e2330' : '#e5e7eb',
    panelBg:     isDark ? '#13151c' : '#ffffff',
    panelBorder: isDark ? '#1e2330' : '#e5e7eb',
    sectionBg:   isDark ? '#1a1d27' : '#f9fafb',
    cardBorder:  isDark ? '#2a2f3e' : '#e5e7eb',
    textPrimary: isDark ? '#f1f5f9' : '#1f2937',
    textSecond:  isDark ? '#94a3b8' : '#6b7280',
    textMuted:   isDark ? '#64748b' : '#9ca3af',
    inputBg:     isDark ? '#1e2330' : '#ffffff',
    inputBorder: isDark ? '#2d3547' : '#d1d5db',
    toggleBg:    isDark ? '#1e2330' : '#f3f4f6',
    segBg:       isDark ? '#1a1d27' : '#f3f4f6',
    segActive:   isDark ? '#2d3547' : '#ffffff',
  };
}