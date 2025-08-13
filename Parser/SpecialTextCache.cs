using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HacknetChineseSupport.Parser
{
    public class SpecialTextCache
    {
        private readonly LinkedList<string> _keys = new LinkedList<string>();
        private readonly Dictionary<string, SpecialFontParserResult> _cache = new Dictionary<string, SpecialFontParserResult>();
        private LinkedListNode<string> curDecCursor = null;
        private const int detectCount = 2;
        private readonly int maxCacheCount = 200;
        private const int cacheExpireSec = 30;

        public SpecialTextCache() {}

        public SpecialTextCache(int maxCacheCount)
        {
            this.maxCacheCount = maxCacheCount;
        }

        public void AddToCache(string key, SpecialFontParserResult result)
        {
            if (_cache.ContainsKey(key))
            {
                _cache[key] = result;
                result.AddToCacheTime = DateTime.Now;
                return;
            }

            if (_keys.Count > maxCacheCount)
            {
                var oldestKey = _keys.First;
                if (oldestKey != null)
                {
                    _keys.RemoveFirst();
                    _cache.Remove(oldestKey.Value);
                }
            }

            _keys.AddLast(key);
            _cache[key] = result;
            result.AddToCacheTime = DateTime.Now;
        }

        private void DetectCacheExpire()
        {
            if (curDecCursor == null)
            {
                if (_keys.Count == 0)
                {
                    return;
                }
                curDecCursor = _keys.First;
            }

            var times = detectCount;
            while (times-- > 0 && curDecCursor != null)
            {
                if (!_cache.TryGetValue(curDecCursor.Value, out var info))
                {
                    if (curDecCursor.List != null)
                    {
                        _keys.Remove(curDecCursor);
                    }
                    continue;
                }
                
                if ((DateTime.Now - info.AddToCacheTime).TotalSeconds > cacheExpireSec)
                {
                    _keys.Remove(curDecCursor);
                    _cache.Remove(curDecCursor.Value);
                }
                curDecCursor = curDecCursor.Next;
            }
        }

        public bool TryGetSpecialTextResult(string key, out SpecialFontParserResult result)
        {
            var res = _cache.TryGetValue(key, out result);
            if (res)
            {
                result.AddToCacheTime = DateTime.Now;
            }
            try
            {
                DetectCacheExpire();
            }
            catch (Exception e)
            {
                HacknetChineseSupportPlugin.Logger.LogError($"DetectCacheExpire error: {e.Message}, threadId:{Thread.CurrentThread.ManagedThreadId}");
                HacknetChineseSupportPlugin.Logger.LogError(e.StackTrace);
            }
            return res;
        }
    }
}
