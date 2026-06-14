export function EmptyState({ title, detail }: { title: string; detail?: string }) {
  return (
    <div className="ui-v2-panel ui-v2-panel-muted">
      <h3>{title}</h3>
      {detail && <p>{detail}</p>}
    </div>
  );
}
