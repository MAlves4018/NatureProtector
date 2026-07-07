export function EmptyState({ title, detail }: { title: string; detail?: string }) {
  return (
    <div className="ui-panel ui-panel-muted">
      <h3>{title}</h3>
      {detail && <p>{detail}</p>}
    </div>
  );
}
