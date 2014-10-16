using System;
using UnityEngine;


namespace CameraTools
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class CamTools : MonoBehaviour
	{
		GameObject cameraParent;
		Vessel vessel;
		Vector3 origPosition;
		Quaternion origRotation;
		Transform origParent;
		float origNearClip;
		FlightCamera flightCamera;
		
		Transform camTarget = null;
		ReferenceModes referenceMode = ReferenceModes.Surface;
		Vector3 cameraUp = Vector3.up;
		
		
		
		//Keys
		string cameraKey = "home";
		string revertKey = "end";
		
		string fmUpKey = "[7]";
		string fmDownKey = "[1]";
		string fmForwardKey = "[8]";
		string fmBackKey = "[5]";
		string fmLeftKey = "[4]";
		string fmRightKey = "[6]";
		string fmZoomInKey = "[9]";
		string fmZoomOutKey = "[3]";
		//
		
		
		//current camera setting
		bool isDefault;
		bool isStationaryCamera = false;
		
		
		//GUI
		public static bool guiEnabled = false;
		public static bool hasAddedButton = false;
		bool updateFOV = false;
		float windowWidth = 250;
		float windowHeight = 400;
		float draggableHeight = 40;
		float leftIndent = 8;
		float entryHeight = 20;
		ToolModes toolMode = ToolModes.StationaryCamera;
		Rect windowRect = new Rect(0,0,0,0);
		bool gameUIToggle = true;
		bool hasFixedWindow = false;
		
		//stationary camera vars
		bool autoFlybyPosition = false;
		bool autoFOV = false;
		float manualFOV = 60;
		float currentFOV = 60;
		Vector3 manualPosition = Vector3.zero;
		float freeMoveSpeed = 10;
		string guiFreeMoveSpeed = "10";
		float keyZoomSpeed = 1;
		string guiKeyZoomSpeed = "1";
		float zoomFactor = 1;
		float zoomExp = 1;
		bool enableKeypad = false;
		float maxRelV = 2500;
		
		bool setPresetOffset = false;
		Vector3 presetOffset = Vector3.zero;
		bool manualOffset = false;
		float manualOffsetForward = 500;
		float manualOffsetRight = 50;
		float manualOffsetUp = 5;
		string guiOffsetForward = "500";
		string guiOffsetRight = "50";
		string guiOffsetUp = "5";
		
		Vector3 lastVesselPosition = Vector3.zero;
		Vector3 lastTargetPosition = Vector3.zero;
		bool hasTarget = false;
		bool useOrbital = false;
		bool hasDied = false;
		float diedTime = 0;
		//vessel reference mode
		Vector3 initialVelocity = Vector3.zero;
		Vector3 initialPosition = Vector3.zero;
		Orbit initialOrbit = null;
		double initialUT;
		
		//retaining position and rotation after vessel destruction
		Vector3 lastPosition;
		Quaternion lastRotation;
		
		
		//click waiting stuff
		bool waitingForTarget = false;
		bool waitingForPosition = false;
		float waitDelayTimer = 0;
			
		//floating origin shift handler
		Vector3d lastOffset = FloatingOrigin.fetch.offset;
		
		void Start()
		{
			
			windowRect = new Rect(Screen.width-windowWidth-5, 45, windowWidth, windowHeight);
			isDefault = true;
			flightCamera = FlightCamera.fetch;
			SaveOriginalCamera();
			
			AddToolbarButton();
			
			GameEvents.onHideUI.Add(GameUIDisable);
			GameEvents.onShowUI.Add(GameUIEnable);
			GameEvents.onGamePause.Add (PostDeathRevert);
			GameEvents.OnVesselRecoveryRequested.Add(PostDeathRevert);
			GameEvents.onFloatingOriginShift.Add(OnFloatingOriginShift);
			
			
			cameraParent = new GameObject();
			cameraParent.SetActive(true);
			cameraParent = (GameObject) Instantiate(cameraParent, Vector3.zero, Quaternion.identity);
			
			if(FlightGlobals.ActiveVessel != null)
			{
				cameraParent.transform.position = FlightGlobals.ActiveVessel.transform.position;
				vessel = FlightGlobals.ActiveVessel;
			
			}
		}
		
		void Update()
		{
			
			
			if(Input.GetKeyDown(KeyCode.KeypadDivide))
			{
				guiEnabled = !guiEnabled;	
			}
			
			if(Input.GetKeyDown(revertKey))
			{
				RevertCamera();	
			}
			else if(Input.GetKeyDown(KeyCode.F9) || Input.GetKey(KeyCode.Escape))
			{
				if(isStationaryCamera) RevertCamera();	
			}
			else if(Input.GetKeyDown(cameraKey))
			{
				if(toolMode == ToolModes.StationaryCamera)
				{
					if(isDefault)
					{
						SaveOriginalCamera();
						StationaryCamera();
					}
					else
					{
						//RevertCamera();
						StationaryCamera();
					}
					
				}
			}
			
			
			//get target transform from mouseClick
			if(waitingForTarget && waitDelayTimer > 0.4f && Input.GetKeyDown(KeyCode.Mouse0))
			{
				Transform tgt = GetTransformFromMouse();
				if(tgt!=null)
				{
					camTarget = tgt;
					hasTarget = true;
				}
				else 
				{
					Vector3 pos = GetPosFromMouse();
					if(pos != Vector3.zero)
					{
						lastTargetPosition = pos;
						hasTarget = true;
					}
				}
				
				waitingForTarget = false;
			}
			
			//set position from mouseClick
			if(waitingForPosition && waitDelayTimer > 0.4f && Input.GetKeyDown(KeyCode.Mouse0))
			{
				Vector3 pos = GetPosFromMouse();
				if(pos!=Vector3.zero)// && isStationaryCamera)
				{
					presetOffset = pos;
					setPresetOffset = true;
				}
				else Debug.Log ("No pos from mouse click");
				
				waitingForPosition = false;
			}
			
			
			
		}
		
		
		void FixedUpdate()
		{
			/*
			if(FloatingOrigin.fetch.offset!=lastOffset)
			{
				lastOffset = FloatingOrigin.fetch.offset;
				OnFloatingOriginShift(lastOffset);
			}
			*/
			
			if(FlightGlobals.ActiveVessel != null && (vessel==null || vessel!=FlightGlobals.ActiveVessel))
			{
				vessel = FlightGlobals.ActiveVessel;
			}
			
			if(vessel!=null)
			{
				lastVesselPosition = vessel.transform.position;
				cameraParent.transform.position = manualPosition + vessel.findWorldCenterOfMass() - vessel.rigidbody.velocity*Time.fixedDeltaTime;	
			}
				
			
			
			//stationary camera
			if(isStationaryCamera)
			{
				if(flightCamera.Target!=null) flightCamera.setTarget(null); //dont go to next vessel if vessel is destroyed
				
				if(camTarget != null)
				{
					Vector3 lookPosition = camTarget.position;
					if(camTarget.rigidbody) lookPosition += camTarget.rigidbody.velocity * Time.fixedDeltaTime;
					flightCamera.transform.rotation = Quaternion.LookRotation(lookPosition - flightCamera.transform.position, cameraUp);
					lastTargetPosition = lookPosition;
				}
				else if(hasTarget) 
				{
					flightCamera.transform.rotation = Quaternion.LookRotation(lastTargetPosition - flightCamera.transform.position, cameraUp);
				}
				
				if(referenceMode == ReferenceModes.Surface && vessel != null)
				{
					flightCamera.transform.position -= Time.fixedDeltaTime * Mathf.Clamp((float)vessel.srf_velocity.magnitude, 0, maxRelV)*vessel.srf_velocity.normalized;
				}
				else if(referenceMode == ReferenceModes.InitialVelocity && vessel != null)
				{
					Vector3 camVelocity = Vector3.zero;
					if(useOrbital && initialOrbit!=null)
					{
						camVelocity = (initialOrbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy - vessel.GetObtVelocity());
					}
					else
					{
						camVelocity = (initialVelocity - vessel.srf_velocity);
					}
					flightCamera.transform.position += camVelocity * Time.fixedDeltaTime;
				}
				
				
				
				//mouse panning, moving
				Vector3 forwardLevelAxis = (Quaternion.AngleAxis(-90, cameraUp) * flightCamera.transform.right).normalized;
				Vector3 rightAxis = (Quaternion.AngleAxis(90, forwardLevelAxis) * cameraUp).normalized;
				
				//free move
				if(enableKeypad)
				{
					if(Input.GetKey (fmUpKey))
					{
						manualPosition += cameraUp * freeMoveSpeed * Time.fixedDeltaTime;	
					}
					else if(Input.GetKey (fmDownKey))
					{
						manualPosition -= 	cameraUp * freeMoveSpeed * Time.fixedDeltaTime;	
					}
					if(Input.GetKey (fmForwardKey))
					{
						manualPosition +=  forwardLevelAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}
					else if(Input.GetKey (fmBackKey))
					{
						manualPosition -= forwardLevelAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}
					if(Input.GetKey (fmLeftKey))
					{
						manualPosition -= flightCamera.transform.right * freeMoveSpeed * Time.fixedDeltaTime;
					}
					else if(Input.GetKey (fmRightKey))
					{
						manualPosition += flightCamera.transform.right * freeMoveSpeed * Time.fixedDeltaTime;
					}
					
					//keyZoom
					if(Input.GetKey (fmZoomInKey))
					{
						zoomExp = Mathf.Clamp(zoomExp + (keyZoomSpeed*Time.fixedDeltaTime), 1, 8);
					}
					else if(Input.GetKey (fmZoomOutKey))
					{
						zoomExp = Mathf.Clamp(zoomExp - (keyZoomSpeed*Time.fixedDeltaTime), 1, 8);
					}
				}
				
				
				if(camTarget == null && Input.GetKey(KeyCode.Mouse1))
				{
					flightCamera.transform.rotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * 1.7f, Vector3.up); //*(Mathf.Abs(Mouse.delta.x)/7)
					flightCamera.transform.rotation *= Quaternion.AngleAxis(-Input.GetAxis("Mouse Y") * 1.7f, Vector3.right);
					flightCamera.transform.rotation = Quaternion.LookRotation(flightCamera.transform.forward, cameraUp);
				}
				if(Input.GetKey(KeyCode.Mouse2))
				{
					manualPosition += flightCamera.transform.right * Input.GetAxis("Mouse X") * 2;
					manualPosition += forwardLevelAxis * Input.GetAxis("Mouse Y") * 2;
				}
				manualPosition += cameraUp * 10 * Input.GetAxis("Mouse ScrollWheel");
				
				//autoFov
				if(camTarget != null && autoFOV)
				{
					float cameraDistance = Vector3.Distance(camTarget.position, flightCamera.transform.position);
					float targetFoV = Mathf.Clamp((7000/(cameraDistance+100)) - 4, 2, 60);
					//flightCamera.SetFoV(targetFoV);	
					manualFOV = targetFoV;
				}
				//FOV
				if(!autoFOV)
				{
					zoomFactor = Mathf.Exp(zoomExp)/Mathf.Exp(1);
					manualFOV = 60/zoomFactor;
					updateFOV = (currentFOV != manualFOV);
					if(updateFOV)
					{
						currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
						flightCamera.SetFoV(currentFOV);
						updateFOV = false;
					}
				}
				else
				{
					currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
					flightCamera.SetFoV(currentFOV);	
				}
				lastPosition = flightCamera.transform.position;
				lastRotation = flightCamera.transform.rotation;
				
			}
			
			
			if(hasDied && Time.time-diedTime > 2)
			{
				RevertCamera();	
			}
			
			//waitDelayTimer increaser..
			if(waitingForTarget || waitingForPosition)
			{
				waitDelayTimer += Time.fixedDeltaTime;	
			}
			
		}
		
		void LateUpdate()
		{
			
			//retain pos and rot after vessel destruction
			if (isStationaryCamera && flightCamera.transform.parent != cameraParent.transform)	
			{
				flightCamera.setTarget(null);
				flightCamera.transform.parent = null;
				flightCamera.transform.position = lastPosition;
				flightCamera.transform.rotation = lastRotation;
				hasDied = true;
				diedTime = Time.time;
			}
			
		}
		
		
		
		
		
		void StationaryCamera()
		{
			if(FlightGlobals.ActiveVessel != null)
			{
				hasDied = false;
				vessel = FlightGlobals.ActiveVessel;
				cameraUp = FlightGlobals.getUpAxis();
				if(FlightCamera.fetch.mode == FlightCamera.Modes.ORBITAL || (FlightCamera.fetch.mode == FlightCamera.Modes.AUTO && FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL))
				{
					cameraUp = Vector3.up;
				}
				
				flightCamera.transform.parent = cameraParent.transform;
				flightCamera.setTarget(null);
				cameraParent.transform.position = vessel.transform.position+vessel.rigidbody.velocity*Time.fixedDeltaTime;
				manualPosition = Vector3.zero;
				if(camTarget!=null) hasTarget = true;
				else hasTarget = false;
				Vector3 rightAxis = Quaternion.AngleAxis(-90, vessel.srf_velocity) * cameraUp;
				if(setPresetOffset)
				{
					flightCamera.transform.position = presetOffset;
					setPresetOffset = false;
				}
				else if(autoFlybyPosition)
				{
					Vector3 clampedVelocity = Mathf.Clamp((float) vessel.srfSpeed, 0, maxRelV) * vessel.srf_velocity.normalized;
					float clampedSpeed = clampedVelocity.magnitude;
					float sideDistance = Mathf.Clamp(20 + (clampedSpeed/10), 20, 150);
					float distanceAhead = Mathf.Clamp(4 * clampedSpeed, 30, 3500);
					
					flightCamera.transform.rotation = Quaternion.LookRotation(vessel.transform.position - flightCamera.transform.position, cameraUp);
					flightCamera.transform.position = vessel.transform.position + (distanceAhead * vessel.srf_velocity.normalized);
					if(flightCamera.mode == FlightCamera.Modes.FREE || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.FREE)
					{
						flightCamera.transform.position += (sideDistance * rightAxis) + (15 * cameraUp);
					}
					else if(flightCamera.mode == FlightCamera.Modes.ORBITAL || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL)
					{
						flightCamera.transform.position += (sideDistance * FlightGlobals.getUpAxis()) + (15 * Vector3.up);
					}
					
				}
				else if(manualOffset)
				{
					float sideDistance = manualOffsetRight;
					float distanceAhead = manualOffsetForward;
					
					
					flightCamera.transform.rotation = Quaternion.LookRotation(vessel.transform.position - flightCamera.transform.position, cameraUp);
					flightCamera.transform.position = vessel.transform.position + (distanceAhead * vessel.srf_velocity.normalized);
					if(flightCamera.mode == FlightCamera.Modes.FREE || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.FREE)
					{
						flightCamera.transform.position += (sideDistance * rightAxis) + (manualOffsetUp * cameraUp);
					}
					else if(flightCamera.mode == FlightCamera.Modes.ORBITAL || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL)
					{
						flightCamera.transform.position += (sideDistance * FlightGlobals.getUpAxis()) + (manualOffsetUp * Vector3.up);
					}
					
				}
				
				initialVelocity = vessel.srf_velocity;
				initialOrbit = new Orbit();
				initialOrbit.UpdateFromStateVectors(vessel.orbit.pos, vessel.orbit.vel, FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
				initialUT = Planetarium.GetUniversalTime();
				
				isStationaryCamera = true;
				isDefault = false;
			}
			else
			{
				Debug.Log ("CTools: Stationary Camera failed. Active Vessel is null.");	
			}
		}
		
		void RevertCamera()
		{
			hasDied = false;
			if(FlightGlobals.ActiveVessel!=null) flightCamera.setTarget(FlightGlobals.ActiveVessel.transform);
			flightCamera.transform.position = origPosition;
			flightCamera.transform.rotation = origRotation;
			flightCamera.transform.parent = origParent;
			flightCamera.SetFoV(60);
			currentFOV = 60;
			Camera.main.nearClipPlane = origNearClip;
			
			
			isDefault = true;
			
			isStationaryCamera = false;
		}
		
		void SaveOriginalCamera()
		{
			origPosition = flightCamera.transform.position;
			origRotation = flightCamera.transform.localRotation;
			origParent = flightCamera.transform.parent;
			origNearClip = Camera.main.nearClipPlane;	
		}
		
		Transform GetTransformFromMouse()
		{
			Vector3 mouseAim = new Vector3(Input.mousePosition.x/Screen.width, Input.mousePosition.y/Screen.height, 0);
			Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, Vessel.loadDistance, 524289))
			{
				return 	hit.transform;
			}
			else return null;
		}
		
		Vector3 GetPosFromMouse()
		{
			Vector3 mouseAim = new Vector3(Input.mousePosition.x/Screen.width, Input.mousePosition.y/Screen.height, 0);
			Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, 5000, 557057))
			{
				return 	hit.point - 10 * ray.direction;
			}
			else return Vector3.zero;
		}
		
		void PostDeathRevert()
		{
			if(isStationaryCamera)	
			{
				RevertCamera();	
			}
		}
		
		void PostDeathRevert(Vessel v)
		{
			if(isStationaryCamera)	
			{
				RevertCamera();	
			}
		}
		
		//GUI
		void OnGUI()
		{
			if(guiEnabled && gameUIToggle) 
			{
				windowRect = GUI.Window(320, windowRect, GuiWindow, "");
			}
		}
				
		void GuiWindow(int windowID)
		{
			GUI.DragWindow(new Rect(0,0,windowWidth, draggableHeight));
			
			GUIStyle centerLabel = new GUIStyle();
			centerLabel.alignment = TextAnchor.UpperCenter;
			centerLabel.normal.textColor = Color.white;
			
			GUIStyle leftLabel = new GUIStyle();
			leftLabel.alignment = TextAnchor.UpperLeft;
			leftLabel.normal.textColor = Color.white;
			
			
			
			int line = 1;
			float contentWidth = (windowWidth) - (2*leftIndent);
			float contentTop = 20;
			GUI.Label(new Rect(0, contentTop, windowWidth, 30), "Camera Tools", centerLabel);
			line++;
			float parseResult;
			//Stationary camera GUI
			if(toolMode == ToolModes.StationaryCamera)
			{	
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Tool: Stationary Camera", leftLabel);
				line++;
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "--------------------------", centerLabel);
				line++;
				
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Frame of Reference: "+referenceMode.ToString(), leftLabel);
				line++;
				if(GUI.Button(new Rect(leftIndent, contentTop+(line*entryHeight), 25, entryHeight-2), "<"))
				{
					CycleRefereneMode(false);
				}
				if(GUI.Button(new Rect(leftIndent+25+4, contentTop+(line*entryHeight), 25, entryHeight-2), ">"))
				{
					CycleRefereneMode(true);
				}
				
				line++;
				
				if(referenceMode == ReferenceModes.Surface)
				{
					GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight),contentWidth/2, entryHeight), "Max Rel. V: ", leftLabel);
					maxRelV = float.Parse(GUI.TextField(new Rect(leftIndent + contentWidth/2, contentTop+(line*entryHeight), contentWidth/2, entryHeight), maxRelV.ToString()));	
				}
				else if(referenceMode == ReferenceModes.InitialVelocity)
				{
					useOrbital = GUI.Toggle(new Rect(leftIndent, contentTop+(line*entryHeight),contentWidth, entryHeight), useOrbital, " Orbital");
				}
				line++;
				line++;
				
					
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Zoom:", leftLabel);
				line++;
				
				zoomExp = GUI.HorizontalSlider(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth - 45, entryHeight), zoomExp, 1, 8);
				
				
				GUI.Label(new Rect(leftIndent+contentWidth-40, contentTop+(line*entryHeight), 40, entryHeight), zoomFactor.ToString("0.0")+"x", leftLabel);
				line++;
				
				autoFOV = GUI.Toggle(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), autoFOV, "Auto Zoom");//, leftLabel);
				line++;
				line++;
				
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Camera Position:", leftLabel);
				line++;
				string posButtonText = "Set Position w/ Click";
				if(setPresetOffset) posButtonText += " (Position Set.)";
				if(waitingForPosition) posButtonText = "waiting...";
				if(FlightGlobals.ActiveVessel!=null && GUI.Button(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight-2), posButtonText))
				{
					waitingForPosition = true;
					waitDelayTimer = 0;
				}
				line++;
				
				if(manualOffset) autoFlybyPosition = false;
				autoFlybyPosition = GUI.Toggle(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), autoFlybyPosition, "Auto Flyby Position");
				line++;
				
				manualOffset = GUI.Toggle(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), manualOffset, "Manual Flyby Position");
				line++;
				if(manualOffset)
				{
					GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), 60, entryHeight), "Fwd:", leftLabel);
					guiOffsetForward = GUI.TextField(new Rect(leftIndent+60, contentTop+(line*entryHeight), (contentWidth/2)-60, entryHeight), guiOffsetForward.ToString());
					if(float.TryParse(guiOffsetForward, out parseResult))
					{
						manualOffsetForward = parseResult;	
					}
					
					GUI.Label(new Rect(leftIndent+(contentWidth/2), contentTop+(line*entryHeight), 60, entryHeight), "Right:", leftLabel);
					guiOffsetRight = GUI.TextField(new Rect(leftIndent+(contentWidth/2)+60, contentTop+(line*entryHeight), (contentWidth/2)-60, entryHeight), guiOffsetRight);
					if(float.TryParse(guiOffsetRight, out parseResult))
					{
						manualOffsetRight = parseResult;	
					}
					
					line++;
					GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), 60, entryHeight), "Up:", leftLabel);
					guiOffsetUp = GUI.TextField(new Rect(leftIndent+60, contentTop+(line*entryHeight), (contentWidth/2)-60, entryHeight), guiOffsetUp);
					if(float.TryParse(guiOffsetUp, out parseResult))
					{
						manualOffsetUp = parseResult;	
					}
					
				}
				else
				{
					line++;
				}
				line++;
				line++;
				
				string targetText = "None";
				if(camTarget!=null) targetText = camTarget.gameObject.name;
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Camera Target: "+targetText, leftLabel);
				line++;
				string tgtButtonText = "Set Target w/ Click";
				if(waitingForTarget) tgtButtonText = "waiting...";
				if(GUI.Button(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight-2), tgtButtonText))
				{
					waitingForTarget = true;
					waitDelayTimer = 0;
				}
				line++;
				if(GUI.Button(new Rect(leftIndent, contentTop+(line*entryHeight), (contentWidth/2)-2, entryHeight-2), "Target Self"))
				{
					camTarget = FlightGlobals.ActiveVessel.transform;
					hasTarget = true;
				}
				if(GUI.Button(new Rect(2+leftIndent+contentWidth/2, contentTop+(line*entryHeight), (contentWidth/2)-2, entryHeight-2), "Clear Target"))
				{
					camTarget = null;
					hasTarget = false;
				}
				line++;
				line++;
				
				enableKeypad = GUI.Toggle(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), enableKeypad, "Keypad Control");
				if(enableKeypad)
				{
					line++;
					
					GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth/2, entryHeight), "Move Speed:");
					guiFreeMoveSpeed = GUI.TextField(new Rect(leftIndent+contentWidth/2, contentTop+(line*entryHeight), contentWidth/2, entryHeight), guiFreeMoveSpeed);
					if(float.TryParse(guiFreeMoveSpeed, out parseResult))
					{
						freeMoveSpeed = Mathf.Abs(parseResult);
						guiFreeMoveSpeed = freeMoveSpeed.ToString();
					}
					
					line++;
					
					GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth/2, entryHeight), "Zoom Speed:");
					guiKeyZoomSpeed = GUI.TextField(new Rect(leftIndent+contentWidth/2, contentTop+(line*entryHeight), contentWidth/2, entryHeight), guiKeyZoomSpeed);
					if(float.TryParse(guiKeyZoomSpeed, out parseResult))
					{
						keyZoomSpeed = Mathf.Abs(parseResult);	
						guiKeyZoomSpeed = keyZoomSpeed.ToString();
					}
				}
				else
				{
					line++;
					line++;
				}
				line++;
				line++;
				
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Keys:", centerLabel);
				line++;
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Activate: ", leftLabel);
				cameraKey = GUI.TextField(new Rect(leftIndent + 60, contentTop+(line*entryHeight), 60, entryHeight-2), cameraKey);
				line++;
				GUI.Label(new Rect(leftIndent, contentTop+(line*entryHeight), contentWidth, entryHeight), "Revert: ", leftLabel);
				revertKey = GUI.TextField(new Rect(leftIndent + 60, contentTop+(line*entryHeight), 60, entryHeight-2), revertKey);
				
				
				
			}
			
			if(!hasFixedWindow)
			{
				windowHeight = contentTop+(line*entryHeight)+entryHeight+entryHeight;
				windowRect = new Rect(Screen.width-windowWidth-5, 45, windowWidth, windowHeight);
				hasFixedWindow = true;
			}
		}
		
		
		//AppLauncherSetup
		void AddToolbarButton()
		{
			if(!hasAddedButton)
			{
				Texture buttonTexture = GameDatabase.Instance.GetTexture("CameraTools/Textures/icon", false);
				ApplicationLauncher.Instance.AddModApplication(EnableGui, DisableGui, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
				CamTools.hasAddedButton = true;
			}
			
		}
		
		void EnableGui()
		{
			guiEnabled = true;
			Debug.Log ("Showing CamTools GUI");
		}
		
		void DisableGui()
		{
			guiEnabled = false;	
			Debug.Log ("Hiding CamTools GUI");
		}
			
		void Dummy()
		{}
		
		void GameUIEnable()
		{
			gameUIToggle = true;	
		}
		
		void GameUIDisable()
		{
			gameUIToggle = false;	
		}
		
		void CycleRefereneMode(bool forward)
		{
			var length = System.Enum.GetValues(typeof(ReferenceModes)).Length;
			if(forward)
			{
				referenceMode++;
				if((int)referenceMode == length) referenceMode = 0;
			}
			else
			{
				referenceMode--;
				if((int)referenceMode == -1) referenceMode = (ReferenceModes) length-1;
			}
		}
		
		void OnFloatingOriginShift(Vector3d offset)
		{
			/*
			Debug.LogWarning ("======Floating origin shifted.======");
			Debug.LogWarning ("======Passed offset: "+offset+"======");
			Debug.LogWarning ("======FloatingOrigin offset: "+FloatingOrigin.fetch.offset+"======");
			Debug.LogWarning("========Floating Origin threshold: "+FloatingOrigin.fetch.threshold+"==========");
			*/
			
		}
		
	}
	
	
	
	public enum ReferenceModes {InitialVelocity, Surface}
	
	public enum ToolModes {StationaryCamera};
}
