﻿/*
 * Copyright (C) 2010, Henon <meinrad.recheis@gmail.com>
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or
 * without modification, are permitted provided that the following
 * conditions are met:
 *
 * - Redistributions of source code must retain the above copyright
 *   notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above
 *   copyright notice, this list of conditions and the following
 *   disclaimer in the documentation and/or other materials provided
 *   with the distribution.
 *
 * - Neither the name of the project nor the
 *   names of its contributors may be used to endorse or promote
 *   products derived from this software without specific prior
 *   written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Linq;
using GitSharp.Core.RevPlot;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace GitSharp.Demo.HistoryGraph
{
    /// <summary>
    /// UserControl that lists Commits and provides selection
    /// </summary>
    public partial class HistoryGraphView : IRepositoryView
    {
        public event Action<Commit> CommitClicked;

        private Repository m_repo;
        private PlotWalk m_revwalk;
        private Window m_WinParent;

        public HistoryGraphView()
        {
            InitializeComponent();
        }

        public void Update(Repository repo)
        {
            m_repo = repo;
            var list = new PlotCommitList();
            m_revwalk = new PlotWalk(repo);
            // if id is not commit, parseCommit() will throw [This should probably be handled better]
            //var CommitIds = ((Core.Repository)repo).getAllRefsByPeeledObjectId().Keys.Where(IsCommit);
            //m_revwalk.markStart(CommitIds.Select(id => m_revwalk.parseCommit(id)));
            m_revwalk.markStart(((Core.Repository)repo).getAllRefsByPeeledObjectId().Keys.Select(id => m_revwalk.parseCommit(id)));
            list.Source(m_revwalk);
            list.fillTo(1000);
            // link the commits
            this.lstCommits.ItemsSource = list;
            // link the jump lists
            try { this.lstBranches.ItemsSource = m_repo.Branches; }
            catch { this.lstBranches.ItemsSource = null; }
            try { this.lstRemotes.ItemsSource = m_repo.RemoteBranches; }
            catch { this.lstRemotes.ItemsSource = null; }
            try { this.lstTags.ItemsSource = m_repo.Tags; }
            catch { this.lstTags.ItemsSource = null; }
            UpdateLegend(); // update refs legend
            this.lstCommits.UpdateLayout(); //fixes issue w/ ScrollIntoView when changing source
        }
        /*private bool IsCommit(GitSharp.Core.AnyObjectId id)
        {
            try { m_revwalk.parseCommit(id); }
            catch { return false; }
            return true;
        }*/

        public void Clear()
        {
            m_repo = null;
            m_revwalk = null;
            this.lstCommits.ItemsSource = null;
            this.imgLegend.Source = null;
            if (CommitClicked != null) CommitClicked(null);
        }

        private void UpdateLegend()
        {
            PlotCommitElement emt = new PlotCommitElement();
            this.imgLegend.Source = new DrawingImage(emt.GetLegend().Drawing);
        }

        private void lstCommits_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateJumpLists();  //Update Jump Lists ?
            PlotCommit commit = lstCommits.SelectedItem as PlotCommit;
            if (CommitClicked == null || commit == null)
                return;
            var c = new Commit(m_repo, commit.Name);
            CommitClicked(c);
        }

        /// <summary>
        /// Gets or Sets the selected commit via its lowercase hexadecimal SHA-1 hash
        /// </summary>
        public string SelectedHash
        {
            get
            {
                PlotCommit commit = lstCommits.SelectedItem as PlotCommit;
                if (commit != null) return commit.Name;
                return "";
            }
            set
            {
                string NewValue = value.ToLower(); //hashes are lowercase, but this value might not
                if (string.Compare(this.SelectedHash, NewValue) == 0) return;
                foreach (PlotCommit cmt in this.lstCommits.Items)
                {
                    if (string.Compare(cmt.Name, NewValue) == 0)
                    {
                        this.lstCommits.SelectedItem = cmt;
                        // Do we assume scroll into view or provide as separate func?
                        ScrollSelectedIntoView();
                        return;
                    }
                }
            }
        }

        public void ScrollSelectedIntoView()
        {
            try { this.lstCommits.ScrollIntoView(this.lstCommits.SelectedItem); }
            catch { }
            /*this.Dispatcher.BeginInvoke((Action)delegate()
            {
                try { this.lstCommits.ScrollIntoView(this.lstCommits.SelectedItem); }
                catch { }
            });*/
        }

        #region Jump Lists Functions
        // (Could use checkbox styled as a button and bind IsChecked/IsOpen?)
        private void cmdJumpList_Click(object sender, RoutedEventArgs e)
        {
            if (this.popJumpList.IsOpen)
            {
                this.popJumpList.IsOpen = false;
                this.cmdJumpList.ClearValue(BackgroundProperty);
            }
            else
            {
                // set size of popup based on current size
                // Could just bind them in XAML ?
                this.popJumpList.Width = this.ActualWidth;
                this.popJumpList.Height = 150;// this.ActualHeight / 2;
                this.popJumpList.IsOpen = true;
                this.cmdJumpList.Background = this.brdJumpList.Background;// Brushes.DarkGray;
            }
        }

        private void UpdateJumpLists()
        {
            this.lstBranches.SelectedItem = null;
            this.lstRemotes.SelectedItem = null;
            this.lstTags.SelectedItem = null;
            /* This would slow down selections, graph already indicates current refs
            string CurHash = this.SelectedHash;
            if (string.IsNullOrEmpty(CurHash)) return;

            // As all hashes are lowercase, keep compares simple case-sensitive
            foreach (KeyValuePair<string,Tag> Tg in this.lstTags.Items)
            {
                if (string.Compare(Tg.Value.Target.Hash, CurHash) == 0)
                {
                    this.lstTags.SelectedItem = Tg;
                    break;
                }
            }
            foreach (KeyValuePair<string,Branch> Br in this.lstBranches.Items)
            {
                if (string.Compare(Br.Value.CurrentCommit.Hash, CurHash) == 0)
                {
                    this.lstBranches.SelectedItem = Br;
                    break;
                }
            }
            foreach (KeyValuePair<string, Branch> Br in this.lstRemotes.Items)
            {
                if (string.Compare(Br.Value.CurrentCommit.Hash, CurHash) == 0)
                {
                    this.lstRemotes.SelectedItem = Br;
                    break;
                }
            }*/
        }

        private void lstTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (this.lstTags.SelectedItem != null)
                {
                    KeyValuePair<string, Tag> v = (KeyValuePair<string, Tag>)this.lstTags.SelectedItem;
                    // assert fails for v.Value.Target with GitSharp repo
                    //if (v.Value.Target != null) this.SelectedHash = v.Value.Target.Hash;
                    //else this.SelectedHash = v.Value.Hash;
                    this.SelectedHash = v.Value.Hash;
                    if (string.Compare(this.SelectedHash, v.Value.Hash) != 0)
                    {
                        if (v.Value.Target != null) this.SelectedHash = v.Value.Target.Hash;
                    }
                }
            }
            catch { }
        }

        private void lstBranches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox LB = sender as ListBox;
            try
            {
                KeyValuePair<string, Branch> Br = (KeyValuePair<string, Branch>)LB.SelectedItem;
                this.SelectedHash = Br.Value.CurrentCommit.Hash;
            }
            catch { }
        }
        #endregion  //Jump Lists Functions

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Need to kill popup on certain events since it floats
            m_WinParent = Window.GetWindow(this);
            if (m_WinParent != null)
            {
                m_WinParent.LocationChanged += new EventHandler(m_WinParent_LocationChanged);
                m_WinParent.Deactivated += new EventHandler(m_WinParent_LocationChanged);
            }
        }

        void m_WinParent_LocationChanged(object sender, EventArgs e)
        {
            //If popup is open, pretend user clicked the toggle
            if (this.popJumpList.IsOpen) cmdJumpList_Click(null, null);
        }


    } // END CLASS: HistoryGraphView

    
    /// <summary>
    /// FrameworkElement that renders a PlotCommit
    /// </summary>
    public class PlotCommitElement : FrameworkElement //Would it be better to ext TextBlock?
    {
        public static DependencyProperty CurrentCommitProperty;
        public PlotCommit CurrentCommit
        {
            get { return (PlotCommit)GetValue(CurrentCommitProperty); }
            set { SetValue(CurrentCommitProperty, value); }
        }

        // Is it more efficient to share one renderer?
        private static DrawingContextPlotRender _Render = null;

        public PlotCommitElement()
        {
            const int LineHeight = 20;
            this.Height = LineHeight;
            if (_Render == null) //only setup renderer once
            {
                // At moment only grabs text styling at creation, could grab dynamically I suppose
                Typeface CurTp = new Typeface((FontFamily)GetValue(TextBlock.FontFamilyProperty),
                    (FontStyle)GetValue(TextBlock.FontStyleProperty), (FontWeight)GetValue(TextBlock.FontWeightProperty),
                    (FontStretch)GetValue(TextBlock.FontStretchProperty));
                _Render = new DrawingContextPlotRender(LineHeight, CurTp);
                //_Render.FontSize = 12; // little bigger than half high text?
            }
        }
        static PlotCommitElement()
        {
            CurrentCommitProperty = DependencyProperty.Register("CurrentCommit", typeof(PlotCommit),
                typeof(PlotCommitElement), new PropertyMetadata(null, new PropertyChangedCallback(OnCurrentCommitChanged)));
        }

        private static void OnCurrentCommitChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            PlotCommitElement that = sender as PlotCommitElement;
            if (that != null)
            {
                that.ToolTip = that.CurrentCommit.getAuthorIdent().ToString();
                that.InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            // Does this force a 2nd draw? Would it be more efficient if fixed size?
            // Should a pre-rendered visual be used?
            this.Width = _Render.DrawPlotCommit(CurrentCommit, dc);
        }

        public DrawingVisual GetLegend() { return _Render.GetLegend(); }
    } // END CLASS: PlotCommitElement

    
    /// <summary>
    /// PlotRenderer that draws to a DrawingContext
    /// </summary>
    public class DrawingContextPlotRender : AbstractPlotRenderer<Brush>
    {
        public int Height;              // Overall Height of the Block we are drawing to
        public Typeface CurTypeface;    // For text/label drawing - Typeface
        public double FontSize;         // For text/label drawing - Font Size
        public double TextMaxWidth;     // For text drawing - limits the width
        public double LabelMaxWidth;    // For label drawing - limits the width
        public int LabelMargin;         // For label drawing - trailing space after label
        public Pen LabelOutline;        // For label drawing - Optional outlie
        private DrawingContext _DC;
        private double _MaxX;

        public DrawingContextPlotRender()
        {
            TextMaxWidth = 0;
            LabelMaxWidth = 0;// 100;   // Should we limit label size or let it ride?
            LabelMargin = 2;
            LabelOutline = new Pen(Brushes.Blue, 1);    // Set to null for none
        }
        public DrawingContextPlotRender(int H, Typeface Tp)
            : this()
        {
            Height = H;
            FontSize = H / 2;   // assume font is half the height
            CurTypeface = Tp;
        }

        /// <summary>
        /// Draws the given PlotCommit to given DrawingContext using class's parameters
        /// </summary>
        /// <param name="Cmt">PlotCommit to render</param>
        /// <param name="CurDC">DrawingContext to use</param>
        /// <returns>Maximum width used</returns>
        public double DrawPlotCommit(PlotCommit Cmt, DrawingContext CurDC)
        {
            if (Cmt == null) return 0;
            _DC = CurDC;    // setup private variables for render process
            _MaxX = 0;
            paintCommit(Cmt, Height);
            _DC = null;     // DC likely won't be valid after this, don't hang on to it
            return _MaxX;
        }

        public DrawingVisual GetLegend()
        {
            DrawingVisual visual = new DrawingVisual();
            int x = 0;
            using (DrawingContext dc = visual.RenderOpen())
            {
                _DC = dc;
                // stuttering, since the first instance will be stripped
                x += DrawLabelInBlock(x, 0, "refs/tags/refs/tags/");
                x += DrawLabelInBlock(x, 0, "refs/heads/refs/heads/");
                x += DrawLabelInBlock(x, 0, "refs/remotes/refs/remotes/");
                x += DrawLabelInBlock(x, 0, "other");
            }
            _DC = null;
            return visual;
        }
        
        // Ref Name type extraction
        public static string GetTagName(string RefName)
        {
            if (string.Compare(RefName, 0, "refs/tags/", 0, 10) == 0)
            {
                return RefName.Substring(10); //return just the unique bit
                //return RefName; //return whole thing
            }
            return null;
        }
        public static string GetHeadName(string RefName)
        {
            if (string.Compare(RefName, 0, "refs/heads/", 0, 11) == 0)
            {
                return RefName.Substring(11); //return just the unique bit
                //return RefName; //return whole thing
            }
            return null;
        }
        public static string GetRemoteName(string RefName)
        {
            if (string.Compare(RefName, 0, "refs/remotes/", 0, 13) == 0)
            {
                return RefName.Substring(13); //return just the unique bit
                //return RefName; //return whole thing
            }
            return null;
        }

        #region Overrides of AbstractPlotRenderer

        protected override int drawLabel(int x, int y, Core.Ref @ref)
        {
            return DrawLabelInBlock(x, y, @ref.Name);
        }

        private int DrawLabelInBlock(int x, int y, string RefName)
        {
            int LabelWidth;
            string PrintName = RefName;
            Brush FillBrush = Brushes.CornflowerBlue; //regular color
            // Render Tags in special way?
            string TagName = GetTagName(PrintName);
            string HeadName = GetHeadName(PrintName);
            string RemoteName = GetRemoteName(PrintName);
            if (TagName != null)
            {
                PrintName = TagName;
                FillBrush = Brushes.DarkGreen; //TAG color
            }
            else if (HeadName != null)
            {
                PrintName = HeadName;
                FillBrush = Brushes.DarkRed; //head color
            }
            else if (RemoteName != null)
            {
                PrintName = RemoteName;
                FillBrush = Brushes.DarkBlue; //remote color
            }

            FormattedText Tx = new FormattedText(PrintName, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, CurTypeface, FontSize, Brushes.White);
            Tx.MaxTextWidth = LabelMaxWidth;    //limit width of label text
            Tx.MaxTextHeight = Height;
            // need to draw color background (oversize rect by one in each direction)
            Point TxOrg = new Point(x - 1, y - FontSize / 2 - 1); // given y is center, need top
            Point TxEnd = new Point(TxOrg.X + Tx.Width + 2, TxOrg.Y + Tx.Height + 2);
            LabelWidth = (int)(TxEnd.X - TxOrg.X + 1);
            _DC.DrawRectangle(FillBrush, LabelOutline, new Rect(TxOrg, TxEnd));
            TxOrg.Offset(1, 1); //draw text inside of background rectangle
            _DC.DrawText(Tx, TxOrg);
            if (_MaxX < TxEnd.X) _MaxX = TxEnd.X; //push out max X
            return LabelWidth + LabelMargin;
        }

        protected override void drawText(string msg, int x, int y)
        {
            FormattedText Tx = new FormattedText(msg, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, CurTypeface, FontSize, Brushes.White);
            Tx.MaxTextWidth = TextMaxWidth;
            Tx.MaxTextHeight = Height;
            double Xend = Tx.Width + x;
            _DC.DrawText(Tx, new Point(x, y - FontSize / 2)); // given y is center, need top
            if (_MaxX < Xend) _MaxX = Xend; //push out max X
        }

        protected override Brush laneColor(PlotLane my_lane)
        {
            return Brushes.Black;
        }

        protected override void drawLine(Brush color, int x1, int y1, int x2, int y2, int width)
        {
            Pen MyPen = new Pen(color, width);
            Point P0 = new Point(x1, y1);
            Point P1 = new Point(x2, y2);
            _DC.DrawLine(MyPen, P0, P1);
        }

        protected override void drawCommitDot(int x, int y, int w, int h)
        {
            double Rx = w / 2;  // convert width/height to radius
            double Ry = h / 2;
            _DC.DrawEllipse(Brushes.Black, null, new Point(x + Rx, y + Ry), Rx, Ry);
        }

        protected override void drawBoundaryDot(int x, int y, int w, int h)
        {
            double Rx = w / 2;  // convert width/height to radius
            double Ry = h / 2;
            _DC.DrawEllipse(Brushes.Red, null, new Point(x + Rx, y + Ry), Rx, Ry);
        }

        #endregion

    } // END CLASS: DrawingContextPlotRender

} // END NAMESPACE
