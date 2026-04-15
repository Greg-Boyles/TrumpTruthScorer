import { Brain, Scale, MessageSquare, Moon, Tag } from 'lucide-react';
import type { DailySummary } from '../types';
import { ScoreBar } from './ScoreBadge';

interface DailySummaryCardProps {
  summary: DailySummary;
}

function formatHour(hour: number): string {
  if (hour === 0) return '12 AM';
  if (hour === 12) return '12 PM';
  if (hour < 12) return `${hour} AM`;
  return `${hour - 12} PM`;
}

function getOverallGrade(score: number): { grade: string; color: string } {
  if (score >= 8) return { grade: 'A', color: 'text-emerald-400' };
  if (score >= 7) return { grade: 'B', color: 'text-blue-400' };
  if (score >= 5) return { grade: 'C', color: 'text-amber-400' };
  if (score >= 3) return { grade: 'D', color: 'text-orange-400' };
  return { grade: 'F', color: 'text-red-400' };
}

export function DailySummaryCard({ summary }: DailySummaryCardProps) {
  const { grade, color } = getOverallGrade(summary.overallScore);

  return (
    <div className="bg-gradient-to-br from-slate-900 to-slate-900/50 border border-slate-800 rounded-2xl p-6">
      {/* Overall Score */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-lg font-semibold text-white mb-1">Daily Summary</h2>
          <p className="text-sm text-slate-500">{summary.totalPosts} posts analyzed</p>
        </div>
        <div className="text-center">
          <div className={`text-5xl font-bold ${color}`}>{grade}</div>
          <div className="text-sm text-slate-500 mt-1">
            {summary.overallScore.toFixed(1)}/10
          </div>
        </div>
      </div>

      {/* Score Bars */}
      <div className="space-y-4 mb-6">
        <ScoreBar 
          score={summary.avgMentalScore} 
          label="Mental State" 
          icon={<Brain className="w-4 h-4" />} 
        />
        <ScoreBar 
          score={summary.avgMoralScore} 
          label="Rhetoric Score" 
          icon={<Scale className="w-4 h-4" />} 
        />
      </div>

      {/* Activity Pattern */}
      <div className="border-t border-slate-800 pt-4 mb-4">
        <div className="flex items-center gap-2 text-sm text-slate-400 mb-3">
          <MessageSquare className="w-4 h-4" />
          <span>Posting Activity</span>
        </div>
        <div className="flex gap-1">
          {Array.from({ length: 24 }).map((_, hour) => {
            const isActive = summary.postingHours.includes(hour);
            return (
              <div
                key={hour}
                className={`flex-1 h-6 rounded-sm ${
                  isActive 
                    ? 'bg-blue-500/60' 
                    : 'bg-slate-800'
                }`}
                title={`${formatHour(hour)}: ${isActive ? 'Active' : 'Quiet'}`}
              />
            );
          })}
        </div>
        <div className="flex justify-between text-xs text-slate-600 mt-1">
          <span>12 AM</span>
          <span>6 AM</span>
          <span>12 PM</span>
          <span>6 PM</span>
          <span>11 PM</span>
        </div>
      </div>

      {/* Quiet Hours */}
      {summary.quietHoursStart !== null && summary.quietHoursEnd !== null && (
        <div className="flex items-center gap-2 text-sm text-slate-400 mb-4">
          <Moon className="w-4 h-4" />
          <span>
            Quiet hours: {formatHour(summary.quietHoursStart)} - {formatHour(summary.quietHoursEnd)}
          </span>
        </div>
      )}

      {/* Top Themes */}
      {summary.topThemes.length > 0 && (
        <div className="border-t border-slate-800 pt-4">
          <div className="flex items-center gap-2 text-sm text-slate-400 mb-2">
            <Tag className="w-4 h-4" />
            <span>Top Themes</span>
          </div>
          <div className="flex flex-wrap gap-2">
            {summary.topThemes.map((theme, i) => (
              <span
                key={i}
                className="text-xs bg-slate-800 text-slate-300 px-2 py-1 rounded capitalize"
              >
                {theme}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
