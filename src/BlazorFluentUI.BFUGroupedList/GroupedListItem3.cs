﻿using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;

namespace BlazorFluentUI
{
    public interface IGroupedListItem3
    {
        bool IsVisible { get;}
        string Name { get; }
        int Count { get; }

    }

    public class HeaderItem3<TItem,TKey> : IGroupedListItem3
    {
        public bool IsOpen { get => isOpenSubject.Value; 
            set 
            {
                isOpenSubject.OnNext(value); 
            }
        }

        private BehaviorSubject<bool> isOpenSubject;
        public IObservable<bool> IsOpenObservable => isOpenSubject.AsObservable();

        public bool IsVisible => true;

        public int Count { get => countSubject.Value;
            set
            {
                countSubject.OnNext(value);
            }
            
        }
        private BehaviorSubject<int> countSubject;
        public IObservable<int> CountChanged => countSubject.AsObservable();

        public int Depth { get; private set; }

        public string Name => _group.Key.ToString();

        public ICollection<IGroupedListItem3> Items { get; private set; }

        private HeaderItem3<TItem, TKey> _parent;
        private IGroup<TItem, TKey, object> _group;

        private IObservable<ISortedChangeSet<IGroupedListItem3, object>> _mainGroupingChangeSet;

        public int GroupIndex { get; set; }

        public void AddGroupAccumulator(IObservable<ISortedChangeSet<IGroupedListItem3, object>> sortedGroupingChangeSet)
        {
            _mainGroupingChangeSet = sortedGroupingChangeSet;

            sortedGroupingChangeSet.Subscribe(x =>
            {
                GroupIndex = x.SortedItems.TakeWhile(x => x.Key != _group.Key).Aggregate(0,(v,x)=>
                {
                    v += x.Value.Count;
                    return v;
                });
            });

            


        }

        public HeaderItem3(IGroup<TItem,TKey,object> group, IEnumerable<Func<TItem,object>> groupBy, int depth, IConnectableObservable<ISortedChangeSet<IGroup<TItem,TKey,object>, object>> groupsChangeSet, HeaderItem3<TItem,TKey> parent)
        {
            _parent = parent;  //needed to get the parent groupindex
            _group = group;
            Depth = depth;
            isOpenSubject = new BehaviorSubject<bool>(true);

            groupsChangeSet.Subscribe(x =>
            {
                GroupIndex = x.SortedItems.TakeWhile(x => x.Key != _group.Key).Aggregate(0, (v, x) =>
                {
                    v += x.Value.Cache.Count;
                    return v;
                });
                if (parent != null)
                {
                    GroupIndex += parent.GroupIndex;
                }
            });

            if (groupBy != null && groupBy.Count() > 0)
            {
                var firstGroup = groupBy.First();
                var rest = groupBy.Skip(1);


                var published = _group.Cache.Connect()
                    .Group(firstGroup)
                    .Sort(SortExpressionComparer<IGroup<TItem, TKey, object>>.Ascending(x => x.Key as IComparable))
                    .Replay();

                    //published.Transform(group => new HeaderItem3<TItem, TKey>(group, rest, depth + 1, published) as IGroupedListItem3)
                    //.Sort(SortExpressionComparer<IGroupedListItem3>.Ascending(x => x.Name as IComparable))
                    //.Replay();

                //futureGroups2.OnNext(published);

                countSubject = new BehaviorSubject<int>(0);
                //published.Transform(x => x.Count);
                published.ToCollection().Subscribe(collection => countSubject.OnNext(collection.Aggregate(0, (v, x) =>
                 {
                     v += x.Cache.Count;
                     return v;
                 })));

                published
                    .Transform(group => new HeaderItem3<TItem, TKey>(group, rest, depth + 1, published, this) as IGroupedListItem3)
                    //.Transform(x =>
                    //{
                    //    //(x as HeaderItem3<TItem, TKey>).AddGroupAccumulator(_mainGroupingChangeSet);
                    //    return x;
                    //})
                    .Bind(out var items)
                    .Subscribe();

                published.Connect();

                Items = items;
            }
            else
            {
                _group.Cache.Connect()
                    .Transform(x => new PlainItem3<TItem, TKey>(x, depth+1) as IGroupedListItem3)
                    .Bind(out var items)
                    .Subscribe();

                countSubject = new BehaviorSubject<int>(items.Count);

                _group.Cache.Connect().QueryWhenChanged(x=> x.Count).Subscribe(x=> countSubject.OnNext(x));
               
                Items = items;
            }

            
        }
    }

    public class PlainItem3<TItem,TKey> : IGroupedListItem3
    {
        //public PlainItem3(TItem item, HeaderItem<TItem> parent, int index, int depth) 

        public PlainItem3(TItem item, int depth)
        {
            this.Item = item;
            this.Depth = depth;
        }
        public int Depth { get; private set; }
        public bool IsVisible { get; set; }

        public int Count => 1;

        public string Name { get; private set; }

        public TItem Item { get; private set; }
    }

    public class GroupedListItem3<TItem>
    {
        private BehaviorSubject<bool> _isVisibleSubject;
        public IObservable<bool> IsVisibleObservable => _isVisibleSubject.AsObservable();
        public bool IsVisible
        {
            get => _isVisibleSubject.Value;
            set
            {
                _isVisibleSubject.OnNext(value);
            }
        }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => _isSelected = value; }

        public TItem Item { get; set; }
        public string Name { get; set; }
        public int Index { get; set; }
        public int Depth { get; set; }
        //public string Key => GetGroupItemKey(this);
        public System.Collections.Generic.List<GroupedListItem<TItem>> Children { get; set; } = new System.Collections.Generic.List<GroupedListItem<TItem>>();

        public int RecursiveCount => Children.RecursiveCount();
        
        

        private static string GetGroupItemKey(GroupedListItem<TItem> groupedListItem)
        {
            string key = "";
            if (groupedListItem.Parent != null)
                key = GetGroupItemKey(groupedListItem.Parent) + "-";
            key += groupedListItem.Index;
            return key;
        }

        public HeaderItem<TItem> Parent { get; set; }

        public GroupedListItem3(TItem item, HeaderItem<TItem> parent, int index, int depth)
        {
            _isVisibleSubject = new BehaviorSubject<bool>(true);

            Item = item;
            Index = index;
            Depth = depth;
            Parent = parent;

            Parent?.IsOpenObservable.CombineLatest(Parent.IsVisibleObservable, (open, visible) => !visible ? false : (open ? true : false)).Subscribe(shouldBeVisible =>
            {
                IsVisible = shouldBeVisible;
            });
        }
    }

    //public class GroupedListItemComparer<TItem> : IComparer<GroupedListItem<TItem>>
    //{
    //    static int compareCount = 0;
    //    public int Compare(GroupedListItem<TItem> x, GroupedListItem<TItem> y)
    //    {
    //        compareCount++;

    //        if (x.Depth == y.Depth) 
    //        {
    //            return CompareItems(x, y);
    //        }
    //        else
    //        {
    //            //not same depth, rewind deeper one until we can compare parents
    //            if (x.Depth < y.Depth)
    //            {
    //                GroupedListItem<TItem> yparent = y;
    //                while (yparent.Depth != x.Depth && yparent.Parent != null)
    //                {
    //                    yparent = yparent.Parent;
    //                }
    //                var comparison = CompareItems(x, yparent);
    //                if (comparison == 0)
    //                {
    //                    return -1;
    //                }
    //                return comparison;
    //            }
    //            else
    //            {
    //                GroupedListItem<TItem> xparent = x;
    //                while (xparent.Depth != y.Depth && xparent.Parent != null)
    //                {
    //                    xparent = xparent.Parent;
    //                }
    //                var comparison = CompareItems(xparent, y);
    //                if (comparison == 0)
    //                {
    //                    return 1;
    //                }
    //                return comparison;
    //            }

    //        }
                        
    //    }

    //    private int CompareItems(GroupedListItem<TItem> x, GroupedListItem<TItem> y)
    //    {
    //        if (x != null & y != null)
    //        {
    //            if (x.Parent == y.Parent)
    //            {
    //                return x.Index.CompareTo(y.Index);
    //            }
    //            else
    //            {
    //                var parentCompare = CompareItems(x.Parent, y.Parent);
    //                if (parentCompare == 0)
    //                {
    //                    return x.Index.CompareTo(y.Index);
    //                }
    //                else
    //                {
    //                    return parentCompare;
    //                }
    //            }
    //        }
    //        else if (x== null && y == null)
    //        {
    //            return 0;
    //        }    
    //        else
    //        {
    //            return 0;
    //        }
    //    }
    //}
}
