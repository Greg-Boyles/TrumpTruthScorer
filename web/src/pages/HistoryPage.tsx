import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, ChevronLeft, ChevronRight } from 'lucide-react';
import { format, subDays, addDays, isFuture } from 'date-fns';
import { usePosts, useDailySummary } from '../hooks/useApi';
import { PostCard } from '../components/PostCard';
import { DailySummaryCard } from '../components/DailySummaryCard';

export function HistoryPage() {
  const [selectedDate, setSelectedDate] = useState(new Date());
  const dateStr = format(selectedDate, 'yyyy-MM-dd');
  
  const { data: postsData, isLoading: postsLoading } = usePosts(dateStr);
  const { data: summaryData, isLoading: summaryLoading } = useDailySummary(dateStr);

  const goToPrevDay = () => setSelectedDate(d => subDays(d, 1));
  const goToNextDay = () => {
    const nextDay = addDays(selectedDate, 1);
    if (!isFuture(nextDay)) {
      setSelectedDate(nextDay);
    }
  };

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
                <h1 className="text-2xl font-bold text-white">History</h1>
                <p className="text-sm text-slate-500">Browse past posts</p>
              </div>
            </div>
            
            {/* Date Navigator */}
            <div className="flex items-center gap-2">
              <button
                onClick={goToPrevDay}
                className="p-2 bg-slate-800 hover:bg-slate-700 rounded-lg transition-colors"
              >
                <ChevronLeft className="w-5 h-5" />
              </button>
              <div className="px-4 py-2 bg-slate-800 rounded-lg min-w-[180px] text-center">
                <span className="font-medium">{format(selectedDate, 'MMMM d, yyyy')}</span>
              </div>
              <button
                onClick={goToNextDay}
                disabled={isFuture(addDays(selectedDate, 1))}
                className="p-2 bg-slate-800 hover:bg-slate-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <ChevronRight className="w-5 h-5" />
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Content */}
      <main className="max-w-6xl mx-auto px-4 py-8">
        <div className="grid lg:grid-cols-3 gap-8">
          {/* Sidebar - Daily Summary */}
          <div className="lg:col-span-1">
            {summaryLoading ? (
              <div className="animate-pulse bg-slate-800 rounded-2xl h-96" />
            ) : summaryData ? (
              <DailySummaryCard summary={summaryData} />
            ) : (
              <div className="bg-slate-900/50 border border-slate-800 rounded-2xl p-6 text-center text-slate-500">
                No summary for this date
              </div>
            )}
          </div>

          {/* Posts List */}
          <div className="lg:col-span-2 space-y-4">
            <h2 className="text-lg font-semibold text-white">
              Posts from {format(selectedDate, 'MMMM d, yyyy')}
            </h2>
            
            {postsLoading ? (
              <div className="space-y-4">
                {[1, 2, 3].map(i => (
                  <div key={i} className="animate-pulse bg-slate-800 rounded-xl h-48" />
                ))}
              </div>
            ) : postsData?.posts && postsData.posts.length > 0 ? (
              <div className="space-y-4">
                {postsData.posts.map(post => (
                  <PostCard key={post.post.postId} data={post} />
                ))}
              </div>
            ) : (
              <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-12 text-center">
                <p className="text-slate-500">No posts found for this date.</p>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}
