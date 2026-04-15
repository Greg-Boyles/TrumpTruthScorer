import { format } from 'date-fns';
import { RefreshCw, Calendar, TrendingUp } from 'lucide-react';
import { Link } from 'react-router-dom';
import { usePosts, useDailySummary } from '../hooks/useApi';
import { PostCard } from '../components/PostCard';
import { DailySummaryCard } from '../components/DailySummaryCard';

export function HomePage() {
  const today = format(new Date(), 'yyyy-MM-dd');
  const { data: postsData, isLoading: postsLoading, refetch } = usePosts();
  const { data: summaryData, isLoading: summaryLoading } = useDailySummary(today);

  return (
    <div className="min-h-screen">
      {/* Header */}
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur-sm sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-bold bg-gradient-to-r from-blue-400 to-purple-500 bg-clip-text text-transparent">
                Truth Scorer
              </h1>
              <p className="text-sm text-slate-500">
                AI analysis of Trump's Truth Social posts
              </p>
            </div>
            <nav className="flex items-center gap-4">
              <Link 
                to="/trends" 
                className="flex items-center gap-2 text-slate-400 hover:text-white transition-colors"
              >
                <TrendingUp className="w-4 h-4" />
                Trends
              </Link>
              <Link 
                to="/history" 
                className="flex items-center gap-2 text-slate-400 hover:text-white transition-colors"
              >
                <Calendar className="w-4 h-4" />
                History
              </Link>
              <button
                onClick={() => refetch()}
                className="flex items-center gap-2 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 rounded-lg text-sm transition-colors"
              >
                <RefreshCw className="w-4 h-4" />
                Refresh
              </button>
            </nav>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-6xl mx-auto px-4 py-8">
        <div className="grid lg:grid-cols-3 gap-8">
          {/* Sidebar - Daily Summary */}
          <div className="lg:col-span-1 space-y-6">
            <div>
              <h2 className="text-sm font-medium text-slate-500 mb-2">
                {format(new Date(), 'EEEE, MMMM d, yyyy')}
              </h2>
              {summaryLoading ? (
                <div className="animate-pulse bg-slate-800 rounded-2xl h-96" />
              ) : summaryData ? (
                <DailySummaryCard summary={summaryData} />
              ) : (
                <div className="bg-slate-900/50 border border-slate-800 rounded-2xl p-6 text-center text-slate-500">
                  No summary available yet
                </div>
              )}
            </div>
          </div>

          {/* Main Feed */}
          <div className="lg:col-span-2 space-y-4">
            <h2 className="text-lg font-semibold text-white">Today's Posts</h2>
            
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
                <p className="text-slate-500">No posts found for today.</p>
                <p className="text-sm text-slate-600 mt-2">
                  Check back later or view historical data.
                </p>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}
