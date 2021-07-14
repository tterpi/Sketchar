namespace Sketching
{
    using UnityEngine;
    using UnityEngine.EventSystems;
    using System.Collections.Generic;
    using VRSketchingGeometry.SketchObjectManagement;
    using VRSketchingGeometry.Commands;
    using VRSketchingGeometry.Commands.Line;
    using UnityEngine.XR.ARFoundation;

    /// <summary>
    /// Controls the creation and deletion of line sketch objects via touch gestures.
    /// </summary>
    public class SketchObjectManipulator : MonoBehaviour
    {
        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR
        /// background).
        /// </summary>
        public Camera FirstPersonCamera;

        /// <summary>
        /// Prefab that is instatiated to create a new line
        /// </summary>
        public GameObject SketchObjectPrefab;

        /// <summary>
        /// The anchor that all sketch objects are attached to
        /// </summary>
        private ARAnchor worldAnchor;

        /// <summary>
        /// The line sketch object that is currently being created.
        /// </summary>
        private LineSketchObject currentLineSketchObject;

        /// <summary>
        /// Used to check if the touch interaction should be performed
        /// </summary>
        private bool canStartTouchManipulation = false;

        /// <summary>
        /// Shows were new control points are added
        /// </summary>
        public GameObject pointMarker;

        /// <summary>
        /// True if a new touch was started, false if a new sketch object was created during this touch
        /// </summary>
        private bool startNewSketchObject = false;

        private CommandInvoker Invoker;

        public SketchWorld SketchWorld;

        public void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            //set up marker in the center of the screen
            pointMarker.transform.SetParent(FirstPersonCamera.transform);
            pointMarker.transform.localPosition = Vector3.forward * .3f;
            Invoker = new CommandInvoker();
        }

        public void Update()
        {
            //handle the touch input
            if (Input.touchCount > 0) {
                Touch currentTouch = Input.GetTouch(0);
                if (currentTouch.phase == TouchPhase.Began) {
                        canStartTouchManipulation = CanStartTouchManipulation();
                }

                if (canStartTouchManipulation) {
                    if (currentTouch.phase == TouchPhase.Began)
                    {
                        startNewSketchObject = true;
                    }
                    else if (currentTouch.phase == TouchPhase.Stationary || (currentTouch.phase == TouchPhase.Moved && startNewSketchObject == false && currentLineSketchObject.getNumberOfControlPoints()>0))
                    {

                        if (startNewSketchObject) {
                            //create a new sketch object
                            CreateNewLineSketchObject();
                            startNewSketchObject = false;
                        }
                        else if (currentLineSketchObject)
                        {
                            //Add new control point according to current device position
                            new AddControlPointContinuousCommand(currentLineSketchObject, FirstPersonCamera.transform.position + FirstPersonCamera.transform.forward * .3f)
                                .Execute();
                        }
                    }
                    else if (currentTouch.phase == TouchPhase.Ended) {
                        //if an empty sketch object was created, delete it
                        if (startNewSketchObject == false && currentLineSketchObject.getNumberOfControlPoints() < 1)
                        {
                            Destroy(currentLineSketchObject.gameObject);
                            currentLineSketchObject = null;
                        }

                        //if a swipe occured and no new sketch object was created, delete the last sketch object
                        if (((startNewSketchObject == false && currentLineSketchObject == null) || startNewSketchObject == true))
                        {
                            if ((currentTouch.position - currentTouch.rawPosition).magnitude > Screen.width * 0.05) {
                                if(Vector2.Dot(Vector2.left, (currentTouch.position - currentTouch.rawPosition)) > 0)
                                {
                                    DeleteLastLineSketchObject();
                                }
                                else if (Vector2.Dot(Vector2.right, (currentTouch.position - currentTouch.rawPosition)) > 0)
                                {
                                    RestoreLastDeletedSketchObject();
                                }
                            }
                        }
                        else {
                            PostProcessSketchObject();
                        }

                        canStartTouchManipulation = false;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a touch interaction can be started
        /// </summary>
        /// <returns></returns>
        private bool CanStartTouchManipulation()
        {
            // Should not handle input if the player is pointing on UI or if the AR session is not tracking the environment.
            if (ARSession.state != ARSessionState.SessionTracking || EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                Debug.Log("Not starting tap gesture");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Instatiates a new LineSketchObject and parants it to the world anchor
        /// </summary>
        private void CreateNewLineSketchObject()
        {
            //see if an anchor exists
            if (!worldAnchor) {
                GameObject anchor = new GameObject();
                anchor.name = "WorldAnchor";
                worldAnchor = anchor.AddComponent<ARAnchor>();
                SketchWorld.transform.SetParent(worldAnchor.transform);
            }

            // Instantiate sketch object as child of anchor
            var gameObject = Instantiate(SketchObjectPrefab);
            currentLineSketchObject = gameObject.GetComponent<LineSketchObject>();
            currentLineSketchObject.minimumControlPointDistance = .02f;
            currentLineSketchObject.SetLineDiameter(.02f);
            currentLineSketchObject.SetInterpolationSteps(5);
        }

        /// <summary>
        /// Refines the latest sketch object
        /// </summary>
        private void PostProcessSketchObject()
        {
            //add the current line sketch object to the stack
            if (currentLineSketchObject != null && currentLineSketchObject.gameObject != null) {
                Invoker.ExecuteCommand(new AddObjectToSketchWorldRootCommand(currentLineSketchObject, SketchWorld));
                if (currentLineSketchObject.getNumberOfControlPoints() > 2) {
                    new RefineMeshCommand(currentLineSketchObject).Execute();
                }
            }
        }

        /// <summary>
        /// Delete the last line sketch object using the Invoker.
        /// </summary>
        public void DeleteLastLineSketchObject() {
            Invoker.Undo();
        }

        public void RestoreLastDeletedSketchObject() {
            Invoker.Redo();
        }
    }
}
