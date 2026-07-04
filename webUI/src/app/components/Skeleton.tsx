interface SkeletonProps {
  width?: string;
  height?: string;
  borderRadius?: string;
  count?: number;
}

export function Skeleton({ width = '100%', height = '16px', borderRadius = '6px', count = 1 }: SkeletonProps) {
  return (
    <span className="ui-skeleton-wrapper" style={{ display: 'inline-flex', flexDirection: 'column', gap: 6 }}>
      {Array.from({ length: count }).map((_, i) => (
        <span
          key={i}
          className="ui-skeleton"
          style={{ width, height, borderRadius, display: 'block' }}
        />
      ))}
    </span>
  );
}
