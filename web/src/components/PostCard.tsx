import { format } from 'date-fns';
import { MessageCircle, Repeat2, Heart, ExternalLink, Tag } from 'lucide-react';
import type { PostWithAnalysis } from '../types';
import { ScoreBadge } from './ScoreBadge';

interface PostCardProps {
  data: PostWithAnalysis;
}

export function PostCard({ data }: PostCardProps) {
  const { post, analysis } = data;
  const createdAt = new Date(post.createdAt);

  return (
    <div className="bg-slate-900/50 border border-slate-800 rounded-xl p-5 card-hover">
      {/* Header */}
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-blue-500 to-purple-600 rounded-full flex items-center justify-center text-white font-bold">
            DT
          </div>
          <div>
            <div className="font-semibold text-white">Donald J. Trump</div>
            <div className="text-sm text-slate-500">
              {format(createdAt, 'MMM d, yyyy · h:mm a')}
            </div>
          </div>
        </div>
        {post.url && (
          <a 
            href={post.url} 
            target="_blank" 
            rel="noopener noreferrer"
            className="text-slate-500 hover:text-blue-400 transition-colors"
          >
            <ExternalLink className="w-4 h-4" />
          </a>
        )}
      </div>

      {/* Content */}
      <p className="text-slate-200 leading-relaxed mb-4 whitespace-pre-wrap">
        {post.content}
      </p>

      {/* Engagement Stats */}
      <div className="flex items-center gap-6 text-sm text-slate-500 mb-4">
        <div className="flex items-center gap-1.5">
          <MessageCircle className="w-4 h-4" />
          <span>{post.repliesCount.toLocaleString()}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Repeat2 className="w-4 h-4" />
          <span>{post.reblogsCount.toLocaleString()}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Heart className="w-4 h-4" />
          <span>{post.favouritesCount.toLocaleString()}</span>
        </div>
        {post.isRetruth && (
          <span className="text-purple-400 text-xs bg-purple-400/10 px-2 py-0.5 rounded">
            Re-Truth
          </span>
        )}
      </div>

      {/* Analysis */}
      {analysis && (
        <div className="border-t border-slate-800 pt-4 mt-4">
          <div className="flex flex-wrap gap-2 mb-3">
            <ScoreBadge score={analysis.mentalScore} type="mental" />
            <ScoreBadge score={analysis.moralScore} type="moral" />
          </div>

          {/* Emotional State */}
          <div className="flex items-center gap-2 text-sm text-slate-400 mb-2">
            <span className="text-slate-500">Tone:</span>
            <span className="capitalize text-slate-300">{analysis.emotionalState}</span>
          </div>

          {/* Summary */}
          <p className="text-sm text-slate-400 italic mb-3">
            "{analysis.summary}"
          </p>

          {/* Themes */}
          {analysis.keyThemes.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {analysis.keyThemes.map((theme, i) => (
                <span 
                  key={i}
                  className="inline-flex items-center gap-1 text-xs bg-slate-800 text-slate-400 px-2 py-1 rounded"
                >
                  <Tag className="w-3 h-3" />
                  {theme}
                </span>
              ))}
            </div>
          )}
        </div>
      )}

      {/* No analysis yet */}
      {!analysis && (
        <div className="border-t border-slate-800 pt-4 mt-4">
          <p className="text-sm text-slate-500 italic">
            Analysis pending...
          </p>
        </div>
      )}
    </div>
  );
}
