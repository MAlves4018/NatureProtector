interface SkeletonProps {
  width?: string;
  height?: string;
  borderRadius?: string;
  count?: number;
}

export function Skeleton({ width = '100%', height = '16px', borderRadius = '6px', count = 1 }: SkeletonProps) {
  return (
    <span className="ui-skeleton-wrapper" style={{ display: 'inline-flex', flexDirection: 'column', gap: 6 }}>
      {Array.from({ length: count }, (_, i) => `skeleton-${i + 1}`).map((key) => (
        <span key={key} className="ui-skeleton" style={{ width, height, borderRadius, display: 'block' }} />
      ))}
    </span>
  );
}
