import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { isUiLocale, translate, type UiLocale, type UiMessageKey } from '../i18n';

interface UiLocaleContextValue {
  locale: UiLocale;
  setLocale: (locale: UiLocale) => void;
  copy: (key: UiMessageKey) => string;
}

const UiLocaleContext = createContext<UiLocaleContextValue | null>(null);

export function UiLocaleProvider({ children }: { children: ReactNode }) {
  const [locale, setLocale] = useState<UiLocale>(() => {
    const stored = sessionStorage.getItem('np.Ui.locale');
    return isUiLocale(stored) ? stored : 'pt-PT';
  });

  const copy = useCallback((key: UiMessageKey) => translate(locale, key), [locale]);

  useEffect(() => {
    sessionStorage.setItem('np.Ui.locale', locale);
  }, [locale]);

  return (
    <UiLocaleContext.Provider value={{ locale, setLocale, copy }}>
      {children}
    </UiLocaleContext.Provider>
  );
}

export function useUiLocale() {
  const context = useContext(UiLocaleContext);
  if (!context) {
    throw new Error('useUiLocale must be used within UiLocaleProvider');
  }
  return context;
}