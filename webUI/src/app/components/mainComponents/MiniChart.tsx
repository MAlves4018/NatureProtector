import {
  AreaChart, Area, BarChart, Bar, LineChart, Line,
  ResponsiveContainer,
} from 'recharts';

export function MiniChart({ data, type, color }: { data:{t:string;v:number}[]; type:string; color:string }) {
  const gid = `g${color.replace('#','')}`;
  if (type==='area') return (
    <ResponsiveContainer width="100%" height={52}>
      <AreaChart data={data} margin={{top:2,right:2,bottom:2,left:2}}>
        <defs><linearGradient id={gid} x1="0" y1="0" x2="0" y2="1">
          <stop offset="5%"  stopColor={color} stopOpacity={0.45}/>
          <stop offset="95%" stopColor={color} stopOpacity={0}/>
        </linearGradient></defs>
        <Area type="monotone" dataKey="v" stroke={color} strokeWidth={2} fill={`url(#${gid})`} dot={false}/>
      </AreaChart>
    </ResponsiveContainer>
  );
  if (type==='bar') return (
    <ResponsiveContainer width="100%" height={52}>
      <BarChart data={data} margin={{top:2,right:2,bottom:2,left:2}}>
        <Bar dataKey="v" fill={color} radius={[2,2,0,0]}/>
      </BarChart>
    </ResponsiveContainer>
  );
  return (
    <ResponsiveContainer width="100%" height={52}>
      <LineChart data={data} margin={{top:2,right:2,bottom:2,left:2}}>
        <Line type="monotone" dataKey="v" stroke={color} strokeWidth={2} dot={false}/>
      </LineChart>
    </ResponsiveContainer>
  );
}