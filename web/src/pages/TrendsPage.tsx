import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useTrends } from '../hooks/useApi';
import { TrendsChart, PostCountChart } from '../components/TrendsChart';

export function TrendsPage() {
  const [days, setDays] = useState(7);
  const { data, isLoading } = useTrends(days);

  return (
    <div className="min-h-screen">
      {/* Header */}
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <Link 
                to="/" 
                className="text-slate-400 hover:text-white transition-colors"
              >
                <ArrowLeft className="w-5 h-5" />
              </Link>
              <div>
                <h1 className="text-2xl font-bold text-white">Trends</h1>
                <p className="text-sm text-slate-500">Historical score analysis</p>
              </div>
            </div>
            <div className="flex gap-2">
              {[7, 14, 30].map(d => (
                <button
                  key={d}
                  onClick={() => setDays(d)}
                  className={`px-3 py-1.5 rounded-lg text-sm transition-colors ${
                    days === d 
                      ? 'bg-blue-500 text-white' 
                      : 'bg-slate-800 text-slate-400 hover:bg-slate-700'
                  }`}
                >
                  {d} days
                </button>
              ))}
            </div>
          </div>
        </div>
      </header>

      {/* Content */}
      <main className="max-w-6xl mx-auto px-4 py-8">
        {isLoading ? (
          <div className="space-y-6">
            <div className="animate-pulse bg-slate-800 rounded-xl h-80" />
            <div className="animate-pulse bg-slate-800 rounded-xl h-56" />
          </div>
        ) : data?.trends && data.trends.length > 0 ? (
          <div className="space-y-6">
            <TrendsChart data={data.trends} />
            <PostCountChart data={data.trends} />

            {/* Summary Stats */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <StatCard 
                label="Avg Mental Score" 
                value={average(data.trends.map(t => t.avgMentalScore)).toFixed(1)}
              />
              <StatCard 
                label="Avg Moral Score" 
                value={average(data.trends.map(t => t.avgMoralScore)).toFixed(1)}
              />
              <StatCard 
                label="Total Posts" 
                value={sum(data.trends.map(t => t.postCount)).toString()}
              />
              <StatCard 
                label="Daily Avg" 
                value={Math.round(average(data.trends.map(t => t.postCount))).toString()}
              />
            </div>
          </div>
        ) : (
          <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-12 text-center">
            <p className="text-slate-500">No trend data available yet.</p>
          </div>
        )}
      </main>
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-4">
      <div className="text-2xl font-bold text-white">{value}</div>
      <div className="text-sm text-slate-500">{label}</div>
    </div>
  );
}

function average(arr: number[]): number {
  if (arr.length === 0) return 0;
  return arr.reduce((a, b) => a + b, 0) / arr.length;
}

function sum(arr: number[]): number {
  return arr.reduce((a, b) => a + b, 0);
}
