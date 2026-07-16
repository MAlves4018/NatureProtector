import { Check, Clipboard, Download } from 'lucide-react';
import { useState } from 'react';

export function ExportActions({
  filename,
  content,
  contentType = 'text/csv;charset=utf-8',
}: {
  filename: string;
  content: string;
  contentType?: string;
}) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    await navigator.clipboard.writeText(content);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  };

  const download = () => {
    const url = URL.createObjectURL(new Blob([content], { type: contentType }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="ui-button-row ui-export-actions">
      <button type="button" className="ui-secondary" onClick={() => void copy()}>
        {copied ? <Check size={15} /> : <Clipboard size={15} />}
        {copied ? 'Copiado' : 'Copiar'}
      </button>
      <button type="button" className="ui-secondary" onClick={download}>
        <Download size={15} /> Exportar
      </button>
    </div>
  );
}
