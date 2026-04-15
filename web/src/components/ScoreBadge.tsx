import { Brain, Scale } from 'lucide-react';

interface ScoreBadgeProps {
  score: number;
  type: 'mental' | 'moral';
  size?: 'sm' | 'md' | 'lg';
}

function getScoreColor(score: number): string {
  if (score >= 8) return 'text-emerald-400 bg-emerald-400/10 border-emerald-400/30';
  if (score >= 6) return 'text-blue-400 bg-blue-400/10 border-blue-400/30';
  if (score >= 4) return 'text-amber-400 bg-amber-400/10 border-amber-400/30';
  return 'text-red-400 bg-red-400/10 border-red-400/30';
}

function getScoreLabel(score: number, type: 'mental' | 'moral'): string {
  if (type === 'mental') {
    if (score >= 8) return 'Calm';
    if (score >= 6) return 'Stable';
    if (score >= 4) return 'Agitated';
    return 'Erratic';
  } else {
    if (score >= 8) return 'Measured';
    if (score >= 6) return 'Standard';
    if (score >= 4) return 'Aggressive';
    return 'Inflammatory';
  }
}

const sizeClasses = {
  sm: 'text-xs px-2 py-0.5 gap-1',
  md: 'text-sm px-3 py-1 gap-1.5',
  lg: 'text-base px-4 py-2 gap-2',
};

export function ScoreBadge({ score, type, size = 'md' }: ScoreBadgeProps) {
  const Icon = type === 'mental' ? Brain : Scale;
  const colorClass = getScoreColor(score);
  const label = getScoreLabel(score, type);

  return (
    <div className={`inline-flex items-center rounded-full border font-medium ${colorClass} ${sizeClasses[size]}`}>
      <Icon className={size === 'sm' ? 'w-3 h-3' : size === 'lg' ? 'w-5 h-5' : 'w-4 h-4'} />
      <span>{score}/10</span>
      <span className="opacity-70">·</span>
      <span className="opacity-80">{label}</span>
    </div>
  );
}

interface ScoreBarProps {
  score: number;
  label: string;
  icon: React.ReactNode;
}

export function ScoreBar({ score, label, icon }: ScoreBarProps) {
  const percentage = (score / 10) * 100;
  const colorClass = getScoreColor(score);
  
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <div className="flex items-center gap-2 text-slate-400">
          {icon}
          <span>{label}</span>
        </div>
        <span className={colorClass.split(' ')[0]}>{score}/10</span>
      </div>
      <div className="h-2 bg-slate-800 rounded-full overflow-hidden">
        <div 
          className={`h-full rounded-full score-bar ${colorClass.split(' ')[1]?.replace('/10', '/40')}`}
          style={{ width: `${percentage}%` }}
        />
      </div>
    </div>
  );
}
