using System;
using System.Collections;
using BenTools.Data;
using System.Collections.Generic;
using FortuneVoronoi.Common;
using MathF = FortuneVoronoi.Tools.Math;

namespace FortuneVoronoi
{
	public class VoronoiGraph
	{
		public HashSet<Point> Vertizes = new HashSet<Point>();
		public HashSet<VoronoiEdge> Edges = new HashSet<VoronoiEdge>();

        public Dictionary<Point, VoronoiCell> Cells { get; set; }
    }
	public class VoronoiEdge
	{
		internal bool Done = false;
		public Point RightData, LeftData;
		public Point VVertexA = Fortune.VVUnkown, VVertexB = Fortune.VVUnkown;
		public void AddVertex(Point V)
		{
			if(VVertexA.IsUndefined)
				VVertexA = V;
			else if(VVertexB.IsUndefined)
				VVertexB = V;
			else throw new Exception("Tried to add third vertex!");
		}
		public bool IsInfinite
		{
			get {return VVertexA == Fortune.VVInfinite && VVertexB == Fortune.VVInfinite;}
		}
		public bool IsPartlyInfinite
		{
            get { return VVertexA == Fortune.VVInfinite || VVertexB == Fortune.VVInfinite; }
		}
		public Point FixedPoint
		{
			get
			{
				if(IsInfinite)
					return (LeftData+RightData) * 0.5;
				if(VVertexA != Fortune.VVInfinite)
					return VVertexA;
				return VVertexB;
			}
		}
		public Point DirectionVector
		{
			get
			{
				if(!IsPartlyInfinite)
					return (VVertexB-VVertexA)*(1.0/Math.Sqrt(Point.DistSqr(VVertexA,VVertexB)));
				if(LeftData.X==RightData.Y)
				{
					if(LeftData.Y<RightData.Y)
						return new Point(-1,0);
					return new Point(1,0);
				}
				Point erg = new Point(-(RightData.Y-LeftData.Y)/(RightData.X-LeftData.X),1);
				if(RightData.X<LeftData.X)
					erg *= -1;
                erg *= 1/erg.Length;
				return erg;
			}
		}
		public double Length
		{
			get
			{
			    return IsPartlyInfinite ? double.PositiveInfinity : Math.Sqrt(Point.DistSqr(VVertexA,VVertexB));
			}
		}
	}
	
	// VoronoiVertex or VoronoiDataPoint are represented as Vector

	internal abstract class VNode
	{
		private VNode _Parent = null;
		private VNode _Left = null, _Right = null;
		public VNode Left
		{
			get{return _Left;}
			set
			{
				_Left = value;
				value.Parent = this;
			}
		}
		public VNode Right
		{
			get{return _Right;}
			set
			{
				_Right = value;
				value.Parent = this;
			}
		}
		public VNode Parent
		{
			get{return _Parent;}
			set{_Parent = value;}
		}


		public void Replace(VNode ChildOld, VNode ChildNew)
		{
			if(Left==ChildOld)
				Left = ChildNew;
			else if(Right==ChildOld)
				Right = ChildNew;
			else throw new Exception("Child not found!");
			ChildOld.Parent = null;
		}

		public static VDataNode FirstDataNode(VNode Root)
		{
			VNode C = Root;
			while(C.Left!=null)
				C = C.Left;
			return (VDataNode)C;
		}
		public static VDataNode LeftDataNode(VDataNode Current)
		{
			VNode C = Current;
			//1. Up
			do
			{
				if(C.Parent==null)
					return null;
				if(C.Parent.Left == C)
				{
					C = C.Parent;
					continue;
				}
				else
				{
					C = C.Parent;
					break;
				}
			}while(true);
			//2. One Left
			C = C.Left;
			//3. Down
			while(C.Right!=null)
				C = C.Right;
			return (VDataNode)C; // Cast statt 'as' damit eine Exception kommt
		}
		public static VDataNode RightDataNode(VDataNode Current)
		{
			VNode C = Current;
			//1. Up
			do
			{
				if(C.Parent==null)
					return null;
				if(C.Parent.Right == C)
				{
					C = C.Parent;
					continue;
				}
				else
				{
					C = C.Parent;
					break;
				}
			}while(true);
			//2. One Right
			C = C.Right;
			//3. Down
			while(C.Left!=null)
				C = C.Left;
			return (VDataNode)C; // Cast statt 'as' damit eine Exception kommt
		}

		public static VEdgeNode EdgeToRightDataNode(VDataNode Current)
		{
			VNode C = Current;
			//1. Up
			do
			{
				if(C.Parent==null)
					throw new Exception("No Left Leaf found!");
				if(C.Parent.Right == C)
				{
					C = C.Parent;
					continue;
				}
				else
				{
					C = C.Parent;
					break;
				}
			}while(true);
			return (VEdgeNode)C;
		}

		public static VDataNode FindDataNode(VNode Root, double ys, double x)
		{
			VNode C = Root;
			do
			{
				if(C is VDataNode)
					return (VDataNode)C;
				if(((VEdgeNode)C).Cut(ys,x)<0)
					C = C.Left;
				else
					C = C.Right;
			}while(true);
		}

		/// <summary>
		/// Will return the new root (unchanged except in start-up)
		/// </summary>
		public static VNode ProcessDataEvent(VDataEvent e, VNode Root, VoronoiGraph VG, double ys, out VDataNode[] CircleCheckList)
		{
			if(Root==null)
			{
				Root = new VDataNode(e.DataPoint);
				CircleCheckList = new VDataNode[] {(VDataNode)Root};
				return Root;
			}
			//1. Find the node to be replaced
			VNode C = VNode.FindDataNode(Root, ys, e.DataPoint.X);
			//2. Create the subtree (ONE Edge, but two VEdgeNodes)
			VoronoiEdge VE = new VoronoiEdge();
			VE.LeftData = ((VDataNode)C).DataPoint;
			VE.RightData = e.DataPoint;
			VE.VVertexA = Fortune.VVUnkown;
			VE.VVertexB = Fortune.VVUnkown;
			VG.Edges.Add(VE);

			VNode SubRoot;
			if(Math.Abs(VE.LeftData.Y-VE.RightData.Y)<1e-10)
			{
				if(VE.LeftData.X<VE.RightData.X)
				{
					SubRoot = new VEdgeNode(VE,false);
					SubRoot.Left = new VDataNode(VE.LeftData);
					SubRoot.Right = new VDataNode(VE.RightData);
				}
				else
				{
					SubRoot = new VEdgeNode(VE,true);
					SubRoot.Left = new VDataNode(VE.RightData);
					SubRoot.Right = new VDataNode(VE.LeftData);
				}
				CircleCheckList = new VDataNode[] {(VDataNode)SubRoot.Left,(VDataNode)SubRoot.Right};
			}
			else
			{
				SubRoot = new VEdgeNode(VE,false);
				SubRoot.Left = new VDataNode(VE.LeftData);
				SubRoot.Right = new VEdgeNode(VE,true);
				SubRoot.Right.Left = new VDataNode(VE.RightData);
				SubRoot.Right.Right = new VDataNode(VE.LeftData);
				CircleCheckList = new VDataNode[] {(VDataNode)SubRoot.Left,(VDataNode)SubRoot.Right.Left,(VDataNode)SubRoot.Right.Right};
			}

			//3. Apply subtree
			if(C.Parent == null)
				return SubRoot;
			C.Parent.Replace(C,SubRoot);
			return Root;
		}
		public static VNode ProcessCircleEvent(VCircleEvent e, VNode Root, VoronoiGraph VG, double ys, out VDataNode[] CircleCheckList)
		{
			VDataNode a,b,c;
			VEdgeNode eu,eo;
			b = e.NodeN;
			a = VNode.LeftDataNode(b);
			c = VNode.RightDataNode(b);
			if(a==null || b.Parent==null || c==null || !a.DataPoint.Equals(e.NodeL.DataPoint) || !c.DataPoint.Equals(e.NodeR.DataPoint))
			{
				CircleCheckList = new VDataNode[]{};
				return Root; // Abbruch da sich der Graph ver�ndert hat
			}
			eu = (VEdgeNode)b.Parent;
			CircleCheckList = new VDataNode[] {a,c};
			//1. Create the new Vertex
			Point VNew = new Point(e.Center.X,e.Center.Y);
//			VNew.X = Fortune.ParabolicCut(a.DataPoint.X,a.DataPoint.Y,c.DataPoint.X,c.DataPoint.Y,ys);
//			VNew.Y = (ys + a.DataPoint.Y)/2 - 1/(2*(ys-a.DataPoint.Y))*(VNew.X-a.DataPoint.X)*(VNew.X-a.DataPoint.X);
			VG.Vertizes.Add(VNew);
			//2. Find out if a or c are in a distand part of the tree (the other is then b's sibling) and assign the new vertex
			if(eu.Left==b) // c is sibling
			{
				eo = VNode.EdgeToRightDataNode(a);

				// replace eu by eu's Right
				eu.Parent.Replace(eu,eu.Right);
			}
			else // a is sibling
			{
				eo = VNode.EdgeToRightDataNode(b);

				// replace eu by eu's Left
				eu.Parent.Replace(eu,eu.Left);
			}
			eu.Edge.AddVertex(VNew);
//			///////////////////// uncertain
//			if(eo==eu)
//				return Root;
//			/////////////////////
			
			//complete & cleanup eo
			eo.Edge.AddVertex(VNew);
			//while(eo.Edge.VVertexB == Fortune.VVUnkown)
			//{
			//    eo.Flipped = !eo.Flipped;
			//    eo.Edge.AddVertex(Fortune.VVInfinite);
			//}
			//if(eo.Flipped)
			//{
			//    Vector T = eo.Edge.LeftData;
			//    eo.Edge.LeftData = eo.Edge.RightData;
			//    eo.Edge.RightData = T;
			//}


			//2. Replace eo by new Edge
			VoronoiEdge VE = new VoronoiEdge();
			VE.LeftData = a.DataPoint;
			VE.RightData = c.DataPoint;
			VE.AddVertex(VNew);
			VG.Edges.Add(VE);

			VEdgeNode VEN = new VEdgeNode(VE, false);
			VEN.Left = eo.Left;
			VEN.Right = eo.Right;
			if(eo.Parent == null)
				return VEN;
			eo.Parent.Replace(eo,VEN);
			return Root;
		}
		public static VCircleEvent CircleCheckDataNode(VDataNode n, double ys)
		{
			VDataNode l = VNode.LeftDataNode(n);
			VDataNode r = VNode.RightDataNode(n);
			if(l==null || r==null || l.DataPoint==r.DataPoint || l.DataPoint==n.DataPoint || n.DataPoint==r.DataPoint)
				return null;
			if(MathF.ccw(l.DataPoint.X,l.DataPoint.Y,n.DataPoint.X,n.DataPoint.Y,r.DataPoint.X,r.DataPoint.Y,false)<=0)
				return null;
			Point Center = Fortune.CircumCircleCenter(l.DataPoint,n.DataPoint,r.DataPoint);
			VCircleEvent VC = new VCircleEvent();
			VC.NodeN = n;
			VC.NodeL = l;
			VC.NodeR = r;
			VC.Center = Center;
			VC.Valid = true;
			if(VC.Y>ys || Math.Abs(VC.Y - ys) < 1e-10)
				return VC;
			return null;
		}

		public static void CleanUpTree(VNode Root)
		{
			if(Root is VDataNode)
				return;
			VEdgeNode VE = Root as VEdgeNode;
			while(VE.Edge.VVertexB == Fortune.VVUnkown)
			{
				VE.Edge.AddVertex(Fortune.VVInfinite);
//				VE.Flipped = !VE.Flipped;
			}
			if(VE.Flipped)
			{
				Point T = VE.Edge.LeftData;
				VE.Edge.LeftData = VE.Edge.RightData;
				VE.Edge.RightData = T;
			}
			VE.Edge.Done = true;
			CleanUpTree(Root.Left);
			CleanUpTree(Root.Right);
		}
	}

	internal class VDataNode : VNode
	{
		public VDataNode(Point DP)
		{
			this.DataPoint = DP;
		}
		public Point DataPoint;
	}

	internal class VEdgeNode : VNode
	{
		public VEdgeNode(VoronoiEdge E, bool Flipped)
		{
			this.Edge = E;
			this.Flipped = Flipped;
		}
		public VoronoiEdge Edge;
		public bool Flipped;
		public double Cut(double ys, double x)
		{
			if(!Flipped)
				return Math.Round(x-Fortune.ParabolicCut(Edge.LeftData.X, Edge.LeftData.Y, Edge.RightData.X, Edge.RightData.Y, ys),10);
			return Math.Round(x-Fortune.ParabolicCut(Edge.RightData.X, Edge.RightData.Y, Edge.LeftData.X, Edge.LeftData.Y, ys),10);
		}
	}


	internal abstract class VEvent : IComparable
	{
		public abstract double Y {get;}
		public abstract double X {get;}
		#region IComparable Members

		public int CompareTo(object obj)
		{
			if(!(obj is VEvent))
				throw new ArgumentException("obj not VEvent!");
			int i = Y.CompareTo(((VEvent)obj).Y);
			if(i!=0)
				return i;
			return X.CompareTo(((VEvent)obj).X);
		}

		#endregion
	}

	internal class VDataEvent : VEvent
	{
		public Point DataPoint;
		public VDataEvent(Point DP)
		{
			this.DataPoint = DP;
		}
		public override double Y
		{
			get
			{
				return DataPoint.Y;
			}
		}

		public override double X
		{
			get
			{
				return DataPoint.X;
			}
		}

	}

	internal class VCircleEvent : VEvent
	{
		public VDataNode NodeN, NodeL, NodeR;
		public Point Center;
		public override double Y
		{
			get
			{
				return Math.Round(Center.Y+MathF.Dist(NodeN.DataPoint.X,NodeN.DataPoint.Y,Center.X,Center.Y),10);
			}
		}

		public override double X
		{
			get
			{
				return Center.X;
			}
		}

		public bool Valid = true;
	}

	public abstract class Fortune
	{
		public static readonly Point VVInfinite = new Point(double.PositiveInfinity, double.PositiveInfinity);
		public static readonly Point VVUnkown = new Point(double.NaN, double.NaN);
		internal static double ParabolicCut(double x1, double y1, double x2, double y2, double ys)
		{
//			y1=-y1;
//			y2=-y2;
//			ys=-ys;
//			
			if(Math.Abs(x1-x2)<1e-10 && Math.Abs(y1-y2)<1e-10)
			{
//				if(y1>y2)
//					return double.PositiveInfinity;
//				if(y1<y2)
//					return double.NegativeInfinity;
//				return x;
				throw new Exception("Identical datapoints are not allowed!");
			}

			if(Math.Abs(y1-ys)<1e-10 && Math.Abs(y2-ys)<1e-10)
				return (x1+x2)/2;
			if(Math.Abs(y1-ys)<1e-10)
				return x1;
			if(Math.Abs(y2-ys)<1e-10)
				return x2;
			double a1 = 1/(2*(y1-ys));
			double a2 = 1/(2*(y2-ys));
			if(Math.Abs(a1-a2)<1e-10)
				return (x1+x2)/2;
			double xs1 = 0.5/(2*a1-2*a2)*(4*a1*x1-4*a2*x2+2*Math.Sqrt(-8*a1*x1*a2*x2-2*a1*y1+2*a1*y2+4*a1*a2*x2*x2+2*a2*y1+4*a2*a1*x1*x1-2*a2*y2));
			double xs2 = 0.5/(2*a1-2*a2)*(4*a1*x1-4*a2*x2-2*Math.Sqrt(-8*a1*x1*a2*x2-2*a1*y1+2*a1*y2+4*a1*a2*x2*x2+2*a2*y1+4*a2*a1*x1*x1-2*a2*y2));
			xs1=Math.Round(xs1,10);
			xs2=Math.Round(xs2,10);
			if(xs1>xs2)
			{
				double h = xs1;
				xs1=xs2;
				xs2=h;
			}
			if(y1>=y2)
				return xs2;
			return xs1;
		}
		internal static Point CircumCircleCenter(Point A, Point B, Point C)
		{
			if(A==B || B==C || A==C)
				throw new Exception("Need three different points!");
			double tx = (A.X + C.X)/2;
			double ty = (A.Y + C.Y)/2;

			double vx = (B.X + C.X)/2;
			double vy = (B.Y + C.Y)/2;

			double ux,uy,wx,wy;
			
			if(A.X == C.X)
			{
				ux = 1;
				uy = 0;
			}
			else
			{
				ux = (C.Y - A.Y)/(A.X - C.X);
				uy = 1;
			}

			if(B.X == C.X)
			{
				wx = -1;
				wy = 0;
			}
			else
			{
				wx = (B.Y - C.Y)/(B.X - C.X);
				wy = -1;
			}

			double alpha = (wy*(vx-tx)-wx*(vy - ty))/(ux*wy-wx*uy);

			return new Point(tx+alpha*ux,ty+alpha*uy);
		}	
		public static VoronoiGraph ComputeVoronoiGraph(Dictionary<Point, VoronoiCell> cells)
		{
			BinaryPriorityQueue PQ = new BinaryPriorityQueue();
			Hashtable CurrentCircles = new Hashtable();
			VoronoiGraph VG = new VoronoiGraph();
			VNode RootNode = null;
			foreach(Point V in cells.Keys)
			{
                PQ.Push(new VDataEvent(V));
			}
			while(PQ.Count>0)
			{
				VEvent VE = PQ.Pop() as VEvent;
				VDataNode[] CircleCheckList;
				if(VE is VDataEvent)
				{
					RootNode = VNode.ProcessDataEvent(VE as VDataEvent,RootNode,VG,VE.Y,out CircleCheckList);
				}
				else if(VE is VCircleEvent)
				{
					CurrentCircles.Remove(((VCircleEvent)VE).NodeN);
					if(!((VCircleEvent)VE).Valid)
						continue;
					RootNode = VNode.ProcessCircleEvent(VE as VCircleEvent,RootNode,VG,VE.Y,out CircleCheckList);
				}
				else throw new Exception("Got event of type "+VE.GetType().ToString()+"!");
				foreach(VDataNode VD in CircleCheckList)
				{
					if(CurrentCircles.ContainsKey(VD))
					{
						((VCircleEvent)CurrentCircles[VD]).Valid=false;
						CurrentCircles.Remove(VD);
					}
					VCircleEvent VCE = VNode.CircleCheckDataNode(VD,VE.Y);
					if(VCE!=null)
					{
						PQ.Push(VCE);
						CurrentCircles[VD]=VCE;
					}
				}
				if(VE is VDataEvent)
				{
					Point DP = ((VDataEvent)VE).DataPoint;
					foreach(VCircleEvent VCE in CurrentCircles.Values)
					{
                        if (MathF.Dist(DP.X, DP.Y, VCE.Center.X, VCE.Center.Y) < VCE.Y - VCE.Center.Y && Math.Abs(MathF.Dist(DP.X, DP.Y, VCE.Center.X, VCE.Center.Y) - (VCE.Y - VCE.Center.Y)) > 1e-10)
							VCE.Valid = false;
					}
				}
			}
			VNode.CleanUpTree(RootNode);
			foreach(VoronoiEdge VE in VG.Edges)
			{
				if(VE.Done)
					continue;
				if(VE.VVertexB.IsUndefined)
				{
					VE.AddVertex(Fortune.VVInfinite);
					if(Math.Abs(VE.LeftData.Y-VE.RightData.Y)<1e-10 && VE.LeftData.X<VE.RightData.X)
					{
						Point T = VE.LeftData;
						VE.LeftData = VE.RightData;
						VE.RightData = T;
					}
				}
			}
			
			ArrayList MinuteEdges = new ArrayList();

            foreach (var edge in VG.Edges)
            {
                double ax = Math.Round(edge.VVertexA.X, Vector.Precision), ay = Math.Round(edge.VVertexA.Y, Vector.Precision),
                       bx = Math.Round(edge.VVertexB.X, Vector.Precision), by = Math.Round(edge.VVertexB.Y, Vector.Precision);
                if (ax == bx && ay == by)
                {
                    MinuteEdges.Add(edge);
                    continue;
                }

                edge.VVertexA = new Point(ax, ay);
                edge.VVertexB = new Point(bx, by);
            }
            
			foreach(VoronoiEdge VE in MinuteEdges)
                VG.Edges.Remove(VE);

            foreach (var edge in VG.Edges)
            {
                VoronoiCell rightCell = cells[edge.RightData], leftCell = cells[edge.LeftData];

                if (!rightCell.Edges.Contains(edge))
                {
                    rightCell.Edges.Add(edge);
                }
                if (!leftCell.Edges.Contains(edge))
                {
                    leftCell.Edges.Add(edge);
                }
            }

            VG.Cells = cells;

			return VG;
		}
		public static VoronoiGraph FilterVG(VoronoiGraph VG, double minLeftRightDist)
		{
			VoronoiGraph VGErg = new VoronoiGraph();
			foreach(VoronoiEdge VE in VG.Edges)
			{
				if(Math.Sqrt(Point.DistSqr(VE.LeftData,VE.RightData))>=minLeftRightDist)
					VGErg.Edges.Add(VE);
			}
			foreach(VoronoiEdge VE in VGErg.Edges)
			{
				VGErg.Vertizes.Add(VE.VVertexA);
				VGErg.Vertizes.Add(VE.VVertexB);
			}
			return VGErg;
		}
	}
}
