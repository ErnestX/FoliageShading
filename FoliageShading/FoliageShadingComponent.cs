using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FoliageShading
{
	public class FoliageShadingComponent : GH_Component
	{
		private bool hasPointsData = false;
		private bool hasResultsData = false;
		private String logOutput = "";
		private int iteration = 0;
		private ShadingsManager shadingsManger = new ShadingsManager();

		/// <summary>
		/// Each implementation of GH_Component must provide a public constructor without any arguments.
		/// Category represents the Tab in which the component will appear, Subcategory the panel. 
		/// If you use non-existing tab or panel names, new tabs/panels will automatically be created.
		/// </summary>
		public FoliageShadingComponent()
		  : base("FoliageShading", "Foliage",
			"Description",
			"Final Project", "Generate Shading")
		{
		}

		protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
		{
			pManager.AddGeometryParameter("Base Surfaces", "BaseSurf", "The areas to fill in with shadings", GH_ParamAccess.list);
			pManager.AddNumberParameter("Interval Distance", "HoriInterval", "Horizontal distance between two shadings, in the model unit", GH_ParamAccess.item);
			pManager.AddNumberParameter("Growth Point Interval", "VertInterval", "Vertical distance between two growth points, influencing density", GH_ParamAccess.item);
			pManager.AddPointParameter("Points for Ladybug Incident Radiation simulation", "SensorPts", "The points where the simulation is done for Ladybug Incident Radiation component", GH_ParamAccess.list);
			pManager.AddNumberParameter("Ladybug Incident Radiation Results", "RadiationResults", "The 'results' output from Ladybug Incident Radiation component", GH_ParamAccess.list);
			pManager.AddIntegerParameter("Points for shading at index", "PFS", "Visuallize simulation grid points locations for each shading by index", GH_ParamAccess.item);

			pManager[3].Optional = true;
			pManager[4].Optional = true;
			pManager[5].Optional = true;
		}

		protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
		{
			pManager.AddCurveParameter("Support Wires", "Wires", "The wires that support the shadings", GH_ParamAccess.list);
			pManager.AddSurfaceParameter("Shadings", "Shadings", "The many pieces of the shadings generated", GH_ParamAccess.list);
			pManager.AddNumberParameter("Grid Size for Ladybug Incident Radiation", "GridSize", "The grid size to use for Ladybug Incident Radiation simulations", GH_ParamAccess.item);
			pManager.AddTextParameter("Log", "Log", "Information about the state of the compoment", GH_ParamAccess.item);
			pManager.AddPointParameter("Points for shading at index", "P", "The Points for shading at selected index for debugging", GH_ParamAccess.list);

			pManager.HideParameter(0);
		}

		/// <param name="DA">
		/// The DA object can be used to retrieve data from input parameters and to store data in output parameters.
		/// </param>
		protected override void SolveInstance(IGH_DataAccess DA)
		{
			List<Surface> baseSurfaces = new List<Surface>();
			Double interval = Double.NaN; 
			Double growthPointInterval = Double.NaN;
			List<Point3d> radiationPoints = new List<Point3d>();
			List<Double> radiationResults = new List<Double>();
			int indexForPointsToVisualize = -1;

			//////////////////// Step1: Access and validate input

			if (!DA.GetDataList(0, baseSurfaces)) return;
			if (!DA.GetData(1, ref interval)) return;
			Constants.intervalDistanceHorizontal = interval;
			if (!DA.GetData(2, ref growthPointInterval)) return;
			Constants.growthPointInterval = growthPointInterval;

			if (DA.GetDataList(3, radiationPoints) && radiationPoints.Count > 1)
			{
				this.hasPointsData = true;
				logOutput += Environment.NewLine + "Points data received: count = " + radiationPoints.Count.ToString();
			}
			else
			{
				this.hasPointsData = false;
				logOutput += Environment.NewLine + "No points data received";
			}

			if (DA.GetDataList(4, radiationResults) && radiationResults.Count > 1)
			{
				this.hasResultsData = true;
				logOutput += Environment.NewLine + "Results data receieved: count = " + radiationResults.Count.ToString();
			}
			else
			{
				this.hasResultsData = false;
				logOutput += Environment.NewLine + "No results data receieved"; 
			}

			DA.GetData(5, ref indexForPointsToVisualize);

			if (interval <= 0)
			{
				AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Interval must be positive");
				return;
			}

			/////////////////// Step2: Create and Manipulate Geometry

			if (!this.shadingsManger.IsInitialized)
			{
				this.shadingsManger.InitializeShadingSurfaces(baseSurfaces, interval, growthPointInterval, Constants.startingShadingDepth);
			}
			else if (this.hasPointsData && this.hasResultsData)
			{
				this.shadingsManger.UpdateSurfacesWithRadiationData(radiationPoints, radiationResults, iteration % 3);
			}

			/////////////////// Step3: Output 
			
			DA.SetDataList(0, this.shadingsManger.CenterLines);

			List<PlaneSurface> shadingsOutput = new List<PlaneSurface>();
			foreach (ShadingSurface ss in this.shadingsManger.ShadingSurfaces)
			{
				shadingsOutput.Add((PlaneSurface)ss.Surface);
			}
			DA.SetDataList(1, shadingsOutput);

			double gridSize = Constants.startingShadingDepth;
			DA.SetData(2, gridSize);

			logOutput += Environment.NewLine + iteration.ToString();
			DA.SetData(3, logOutput);

			if (indexForPointsToVisualize > -1)
			{
				List<Point3d> pointsToVisualize = this.shadingsManger.ShadingSurfaces[indexForPointsToVisualize].LastSensorPoints;
				DA.SetDataList(4, pointsToVisualize);
			}

			iteration += 1;
		}

		/// <summary>
		/// The Exposure property controls where in the panel a component icon will appear. 
		/// There are seven possible locations (primary to septenary), each of which can be combined with the GH_Exposure.obscure flag, 
		/// which ensures the component will only be visible on panel dropdowns.
		/// </summary>
		public override GH_Exposure Exposure => GH_Exposure.primary;

		/// <summary>
		/// Provides an Icon for every component that will be visible in the User Interface. Icons need to be 24x24 pixels.
		/// You can add image files to your project resources and access them like this: return Resources.IconForThisComponent;
		/// </summary>
		protected override System.Drawing.Bitmap Icon => null;

		/// <summary>
		/// Each component must have a unique Guid to identify it. 
		/// It is vital this Guid doesn't change otherwise old ghx files that use the old ID will partially fail during loading.
		/// </summary>
		public override Guid ComponentGuid => new Guid("3de06452-1374-475f-8a9f-b54cf4b94e09");
	}
}