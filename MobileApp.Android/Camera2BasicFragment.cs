﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Hardware.Camera2;
using Android.Graphics;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.Support.V13.App;
using Android.Support.V4.Content;
using Camera2Basic.Listeners;
using Java.Lang;
using Java.Util;
using Java.Util.Concurrent;
using Boolean = Java.Lang.Boolean;
using Math = Java.Lang.Math;
using Orientation = Android.Content.Res.Orientation;
using Random = System.Random;
using Semaphore = Java.Util.Concurrent.Semaphore;
using System.Timers;
using MeterReader;
using OpenCvSharp;
using File = Java.IO.File;
using Point = Android.Graphics.Point;
using Rect = Android.Graphics.Rect;
using Size = Android.Util.Size;
using Stream = System.IO.Stream;
using Plugin.Messaging;
using System.Text.RegularExpressions;

namespace Camera2Basic
{
    public class Camera2BasicFragment : Fragment, FragmentCompat.IOnRequestPermissionsResultCallback, View.IOnClickListener
    {
        public static readonly int REQUEST_CAMERA_PERMISSION = 1;
        private static readonly string FRAGMENT_DIALOG = "dialog";
        private static readonly string EMAIL_TEMPLATE_NAME = "email_template_meter_reader";

        // Tag for the {@link Log}.
        private static readonly string TAG = "Camera2BasicFragment";

        // Camera state: Showing camera preview.
        public const int STATE_PREVIEW = 0;

        // Camera state: Waiting for the focus to be locked.
        public const int STATE_WAITING_LOCK = 1;

        // Camera state: Waiting for the exposure to be precapture state.
        public const int STATE_WAITING_PRECAPTURE = 2;

        //Camera state: Waiting for the exposure state to be something other than precapture.
        public const int STATE_WAITING_NON_PRECAPTURE = 3;

        // Camera state: Picture was taken.
        public const int STATE_PICTURE_TAKEN = 4;

        // Max preview width that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_WIDTH = 1920;

        // Max preview height that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_HEIGHT = 1080;

        // TextureView.ISurfaceTextureListener handles several lifecycle events on a TextureView
        private Camera2BasicSurfaceTextureListener mSurfaceTextureListener;

        // ID of the current {@link CameraDevice}.
        private string mCameraId;

        // An AutoFitTextureView for camera preview
        private TextureView _cameraPreviewTexture;

        // A {@link CameraCaptureSession } for camera preview.
        public CameraCaptureSession mCaptureSession;

        // A reference to the opened CameraDevice
        public CameraDevice mCameraDevice;

        // The size of the camera preview
        private Size mPreviewSize;

        // CameraDevice.StateListener is called when a CameraDevice changes its state
        private CameraStateListener mStateCallback;

        // An additional thread for running tasks that shouldn't block the UI.
        private HandlerThread mBackgroundThread;

        // A {@link Handler} for running tasks in the background.
        public Handler mBackgroundHandler;

        //{@link CaptureRequest.Builder} for the camera preview
        public CaptureRequest.Builder mPreviewRequestBuilder;

        // {@link CaptureRequest} generated by {@link #mPreviewRequestBuilder}
        public CaptureRequest mPreviewRequest;

        // The current state of camera state for taking pictures.
        public int mState = STATE_PREVIEW;

        // A {@link Semaphore} to prevent the app from exiting before closing the camera.
        public Semaphore mCameraOpenCloseLock = new Semaphore(1);

        // Whether the current camera device supports Flash or not.
        private bool mFlashSupported;

        // Orientation of the camera sensor
        private int mSensorOrientation;

        // A {@link CameraCaptureSession.CaptureCallback} that handles events related to JPEG capture.
        public CameraCaptureListener mCaptureCallback;

        private NumberRectanglesView mNumberRectanglesView;

        private CaptureRectangleView mCaptureRectangleView;

        private ImageView mSectorColorToggleButton;

        private Random mRandom = new Random();
        private TextView mTextView;
        private MeterReader.Xamarin.MeterReader mMeterReader;
        private bool mSectorsAreDark = true;
        private LayoutInflater mLayoutInflater;
        private AlertDialog mConfirmationModal;

        private List<string> mReadings = new List<string>();
        private AlertDialog mEmailTemplateModal;
        private EmailSettings mEmailSettings;
        private ReadingAccuracyModule mAccuracyModule;

        private bool mFlashLightOn = false;

        // Shows a {@link Toast} on the UI thread.
        public void ShowToast(string text)
        {
            if (Activity != null)
            {
                Activity.RunOnUiThread(new ShowToastRunnable(Activity.ApplicationContext, text));
            }
        }

        private class ShowToastRunnable : Java.Lang.Object, IRunnable
        {
            private string text;
            private Context context;

            public ShowToastRunnable(Context context, string text)
            {
                this.context = context;
                this.text = text;
            }

            public void Run()
            {
                Toast.MakeText(context, text, ToastLength.Short).Show();
            }
        }

        public static Camera2BasicFragment NewInstance()
        {
            return new Camera2BasicFragment();
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            mStateCallback = new CameraStateListener(this);
            mSurfaceTextureListener = new Camera2BasicSurfaceTextureListener(this);
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            mEmailSettings = LoadSettings();
            mAccuracyModule = new ReadingAccuracyModule(3);

            mLayoutInflater = inflater;
            var view = inflater.Inflate(Resource.Layout.fragment_camera2_basic, container, false);

            mCaptureRectangleView = new CaptureRectangleView(this.Context);
            mNumberRectanglesView = new NumberRectanglesView(Context, mCaptureRectangleView);

            var layout = (RelativeLayout)view.FindViewById(Resource.Id.mainLayout);
            layout.AddView(mNumberRectanglesView);
            layout.AddView(mCaptureRectangleView);

            mSectorColorToggleButton = (ImageView)view.FindViewById(Resource.Id.sectorColorToggleButton);
            RefreshSectorColorButtonResource();
            mSectorColorToggleButton.SetOnClickListener(this);

            ((ImageView)view.FindViewById(Resource.Id.emailTemplateButton)).SetOnClickListener(this);
            ((ImageView)view.FindViewById(Resource.Id.flashlightToggleButton)).SetOnClickListener(this);


            _cameraPreviewTexture = (TextureView)view.FindViewById(Resource.Id.texture);
            mTextView = (TextView)view.FindViewById(Resource.Id.textView);


            return view;
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            Task.Factory.StartNew(() => { while (true) { TimerTick(); } }, TaskCreationOptions.LongRunning);
        }


        private async void TimerTick()
        {
            if (mCaptureRectangleView.CaptureRectangle.Width == 0)
            {
                return;
            }
            try
            {
                var analyzeRectangle = mCaptureRectangleView.CaptureRectangle;
                using (var croppedImage = Bitmap.CreateBitmap(_cameraPreviewTexture.Bitmap, analyzeRectangle.X, analyzeRectangle.Y, analyzeRectangle.Width, analyzeRectangle.Height))
                {
                    using (var imageStream = BitmapToStream(croppedImage))
                    {
                        var mat = Mat.FromStream(imageStream, ImreadModes.AnyColor);
                        var meterResults = await mMeterReader.Analyze(mat);
                        if (meterResults.Success)
                        {
                            mAccuracyModule.AddReading(meterResults.Result);
                        }
                        Activity.RunOnUiThread(() =>
                        {
                            if(mAccuracyModule.AccuracyAchieved)
                            {
                                ShowConfirmationModal(mAccuracyModule.GetReading(), mat);
                            }
                            mNumberRectanglesView.UpdateData(meterResults.Rectangles);
                        });
                    }
                }
            }
            catch (System.Exception ex)
            {
            }
            return;
        }

        private Stream BitmapToStream(Bitmap input)
        {
            var stream = new MemoryStream();
            input.Compress(Bitmap.CompressFormat.Png, 0, stream);
            stream.Position = 0;
            return stream;
        }

        private byte[] BitmapToByteArray(Bitmap input)
        {
            var stream = new MemoryStream();
            input.Compress(Bitmap.CompressFormat.Png, 0, stream);
            stream.Position = 0;
            return stream.ToArray();
        }

        public override async void OnActivityCreated(Bundle savedInstanceState)
        {
            base.OnActivityCreated(savedInstanceState);
            mCaptureCallback = new CameraCaptureListener(this);

            var meterSettings = new MeterReader.Xamarin.MeterReaderSettings(new Tesseract.Droid.TesseractApi(Context, Tesseract.Droid.AssetsDeployment.OncePerInitialization))
            {
                DarkSectors = mSectorsAreDark
            };
            mMeterReader = new MeterReader.Xamarin.MeterReader(meterSettings);
            await mMeterReader.Init();
        }

        public override void OnResume()
        {
            base.OnResume();
            StartBackgroundThread();

            // When the screen is turned off and turned back on, the SurfaceTexture is already
            // available, and "onSurfaceTextureAvailable" will not be called. In that case, we can open
            // a camera and start preview from here (otherwise, we wait until the surface is ready in
            // the SurfaceTextureListener).
            if (_cameraPreviewTexture.IsAvailable)
            {
                OpenCamera(_cameraPreviewTexture.Width, _cameraPreviewTexture.Height);
            }
            else
            {
                _cameraPreviewTexture.SurfaceTextureListener = mSurfaceTextureListener;
            }
        }

        public override void OnPause()
        {
            CloseCamera();
            StopBackgroundThread();
            base.OnPause();
        }

        private void RequestCameraPermission()
        {
            if (FragmentCompat.ShouldShowRequestPermissionRationale(this, Manifest.Permission.Camera))
            {
                new ConfirmationDialog().Show(ChildFragmentManager, FRAGMENT_DIALOG);
            }
            else
            {
                FragmentCompat.RequestPermissions(this, new string[] { Manifest.Permission.Camera },
                    REQUEST_CAMERA_PERMISSION);
            }
        }

        public void OnRequestPermissionsResult(int requestCode, string[] permissions, int[] grantResults)
        {
            if (requestCode != REQUEST_CAMERA_PERMISSION)
                return;

            if (grantResults.Length != 1 || grantResults[0] != (int)Permission.Granted)
            {
                ErrorDialog.NewInstance(GetString(Resource.String.request_permission))
                    .Show(ChildFragmentManager, FRAGMENT_DIALOG);
            }
        }


        // Sets up member variables related to camera.
        private void SetUpCameraOutputs(int width, int height)
        {
            var activity = Activity;
            var manager = (CameraManager)activity.GetSystemService(Context.CameraService);
            try
            {
                for (var i = 0; i < manager.GetCameraIdList().Length; i++)
                {
                    var cameraId = manager.GetCameraIdList()[i];
                    CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraId);

                    // We don't use a front facing camera in this sample.
                    var facing = (Integer)characteristics.Get(CameraCharacteristics.LensFacing);
                    if (facing != null && facing == (Integer.ValueOf((int)LensFacing.Front)))
                    {
                        continue;
                    }

                    var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics
                        .ScalerStreamConfigurationMap);
                    if (map == null)
                    {
                        continue;
                    }

                    // Find out if we need to swap dimension to get the preview size relative to sensor
                    // coordinate.
                    var displayRotation = activity.WindowManager.DefaultDisplay.Rotation;
                    //noinspection ConstantConditions
                    mSensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);
                    bool swappedDimensions = false;
                    switch (displayRotation)
                    {
                        case SurfaceOrientation.Rotation0:
                        case SurfaceOrientation.Rotation180:
                            if (mSensorOrientation == 90 || mSensorOrientation == 270)
                            {
                                swappedDimensions = true;
                            }

                            break;
                        case SurfaceOrientation.Rotation90:
                        case SurfaceOrientation.Rotation270:
                            if (mSensorOrientation == 0 || mSensorOrientation == 180)
                            {
                                swappedDimensions = true;
                            }

                            break;
                        default:
                            Log.Error(TAG, "Display rotation is invalid: " + displayRotation);
                            break;
                    }

                    Point displaySize = new Point();
                    activity.WindowManager.DefaultDisplay.GetSize(displaySize);
                    var rotatedPreviewWidth = width;
                    var rotatedPreviewHeight = height;
                    var maxPreviewWidth = displaySize.X;
                    var maxPreviewHeight = displaySize.Y;

                    if (swappedDimensions)
                    {
                        rotatedPreviewWidth = height;
                        rotatedPreviewHeight = width;
                        maxPreviewWidth = displaySize.Y;
                        maxPreviewHeight = displaySize.X;
                    }

                    if (maxPreviewWidth > MAX_PREVIEW_WIDTH)
                    {
                        maxPreviewWidth = MAX_PREVIEW_WIDTH;
                    }

                    if (maxPreviewHeight > MAX_PREVIEW_HEIGHT)
                    {
                        maxPreviewHeight = MAX_PREVIEW_HEIGHT;
                    }

                    // Danger, W.R.! Attempting to use too large a preview size could  exceed the camera
                    // bus' bandwidth limitation, resulting in gorgeous previews but the storage of
                    // garbage capture data.
                    mPreviewSize = new Size(960, 540);

                    // Check if the flash is supported.
                    var available = (Boolean)characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
                    if (available == null)
                    {
                        mFlashSupported = false;
                    }
                    else
                    {
                        mFlashSupported = (bool)available;
                    }

                    mCameraId = cameraId;
                    return;
                }
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (NullPointerException e)
            {
                // Currently an NPE is thrown when the Camera2API is used but not supported on the
                // device this code runs.
                ErrorDialog.NewInstance(GetString(Resource.String.camera_error))
                    .Show(ChildFragmentManager, FRAGMENT_DIALOG);
            }
        }

        // Opens the camera specified by {@link Camera2BasicFragment#mCameraId}.
        public void OpenCamera(int width, int height)
        {
            if (ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.Camera) != Permission.Granted)
            {
                RequestCameraPermission();
                return;
            }

            SetUpCameraOutputs(width, height);
            ConfigureTransform(width, height);
            var activity = Activity;
            var manager = (CameraManager)activity.GetSystemService(Context.CameraService);
            try
            {
                if (!mCameraOpenCloseLock.TryAcquire(5000, TimeUnit.Milliseconds))
                {
                    throw new RuntimeException("Time out waiting to lock camera opening.");
                }

                manager.OpenCamera(mCameraId, mStateCallback, mBackgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera opening.", e);
            }
        }

        // Closes the current {@link CameraDevice}.
        private void CloseCamera()
        {
            try
            {
                mCameraOpenCloseLock.Acquire();
                if (null != mCaptureSession)
                {
                    mCaptureSession.Close();
                    mCaptureSession = null;
                }

                if (null != mCameraDevice)
                {
                    mCameraDevice.Close();
                    mCameraDevice = null;
                }
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera closing.", e);
            }
            finally
            {
                mCameraOpenCloseLock.Release();
            }
        }

        // Starts a background thread and its {@link Handler}.
        private void StartBackgroundThread()
        {
            mBackgroundThread = new HandlerThread("CameraBackground");
            mBackgroundThread.Start();
            mBackgroundHandler = new Handler(mBackgroundThread.Looper);
        }

        // Stops the background thread and its {@link Handler}.
        private void StopBackgroundThread()
        {
            mBackgroundThread.QuitSafely();
            try
            {
                mBackgroundThread.Join();
                mBackgroundThread = null;
                mBackgroundHandler = null;
            }
            catch (InterruptedException e)
            {
                e.PrintStackTrace();
            }
        }

        // Creates a new {@link CameraCaptureSession} for camera preview.
        public void CreateCameraPreviewSession()
        {
            try
            {
                SurfaceTexture texture = _cameraPreviewTexture.SurfaceTexture;
                if (texture == null)
                {
                    throw new IllegalStateException("texture is null");
                }

                // We configure the size of default buffer to be the size of camera preview we want.
                texture.SetDefaultBufferSize(mPreviewSize.Width, mPreviewSize.Height);

                // This is the output Surface we need to start preview.
                Surface surface = new Surface(texture);

                // We set up a CaptureRequest.Builder with the output Surface.
                mPreviewRequestBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
                mPreviewRequestBuilder.AddTarget(surface);

                // Here, we create a CameraCaptureSession for camera preview.
                List<Surface> surfaces = new List<Surface>();
                surfaces.Add(surface);
                mCameraDevice.CreateCaptureSession(surfaces, new CameraCaptureSessionCallback(this), null);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public static T Cast<T>(Java.Lang.Object obj) where T : class
        {
            var propertyInfo = obj.GetType().GetProperty("Instance");
            return propertyInfo == null ? null : propertyInfo.GetValue(obj, null) as T;
        }

        // Configures the necessary {@link android.graphics.Matrix}
        // transformation to `mTextureView`.
        // This method should be called after the camera preview size is determined in
        // setUpCameraOutputs and also the size of `mTextureView` is fixed.
        public void ConfigureTransform(int viewWidth, int viewHeight)
        {
            Activity activity = Activity;
            if (null == _cameraPreviewTexture || null == mPreviewSize || null == activity)
            {
                return;
            }

            var rotation = (int)activity.WindowManager.DefaultDisplay.Rotation;
            Matrix matrix = new Matrix();
            RectF viewRect = new RectF(0, 0, viewWidth, viewHeight);
            RectF bufferRect = new RectF(0, 0, mPreviewSize.Height, mPreviewSize.Width);
            float centerX = viewRect.CenterX();
            float centerY = viewRect.CenterY();
            if ((int)SurfaceOrientation.Rotation90 == rotation || (int)SurfaceOrientation.Rotation270 == rotation)
            {
                bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);
                float scale = Math.Max((float)viewHeight / mPreviewSize.Height,
                    (float)viewWidth / mPreviewSize.Width);
                matrix.PostScale(scale, scale, centerX, centerY);
                matrix.PostRotate(90 * (rotation - 2), centerX, centerY);
            }
            else if ((int)SurfaceOrientation.Rotation180 == rotation)
            {
                matrix.PostRotate(180, centerX, centerY);
            }

            _cameraPreviewTexture.SetTransform(matrix);
        }

        public void OnClick(View v)
        {
            if (v.Id == Resource.Id.sectorColorToggleButton)
            {
                this.mSectorsAreDark = !this.mSectorsAreDark;
                RefreshSectorColorButtonResource();
                mMeterReader.SetSectorColor(this.mSectorsAreDark);
            }
            if (v.Id == Resource.Id.emailTemplateButton)
            {
                ShowEmailTemplateModal();
            }
            if (v.Id == Resource.Id.flashlightToggleButton)
            {
                ToggleFlashLight();
            }
        }

        private void ToggleFlashLight()
        {
            if (!mFlashLightOn)
            {
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Torch);
                mFlashLightOn = true;
            }
            else
            {
                mPreviewRequestBuilder.Set(CaptureRequest.FlashMode, (int)FlashMode.Off);
                mFlashLightOn = false;
            }
            mPreviewRequest = mPreviewRequestBuilder.Build();
            mCaptureSession.SetRepeatingRequest(mPreviewRequest, mCaptureCallback, mBackgroundHandler);
        }

        private void ShowConfirmationModal(string reading, Mat image)
        {
            if (mConfirmationModal?.IsShowing == true)
            {
                return;
            }
            var alert = new AlertDialog.Builder(Context);
            var view = mLayoutInflater.Inflate(Resource.Layout.confirmation, null);
            view.FindViewById<Button>(Resource.Id.cancelReadingButton).Click += (object sender, EventArgs e) =>
            {
                Toast.MakeText(Context, "Katkestatud", ToastLength.Short).Show();
                mReadings.Clear();
                mConfirmationModal.Dismiss();
                UpdateReadingsText();
            };
            if(mEmailSettings.NumberOfRequiredReadings > mReadings.Count + 1)
            {
                var button = view.FindViewById<Button>(Resource.Id.captureAnotherReadingButton);
                button.Visibility = ViewStates.Visible;
                button.Click += (object sender, EventArgs e) =>
                {
                    var confirmedReading = mConfirmationModal.FindViewById<EditText>(Resource.Id.readingEditText).Text;
                    mReadings.Add(confirmedReading);
                    mConfirmationModal.Dismiss();
                    UpdateReadingsText();
                };
            }
            else
            {
                var button = view.FindViewById<Button>(Resource.Id.sendEmailButton);
                button.Visibility = ViewStates.Visible;
                button.Click += (object sender, EventArgs e) =>
                {
                    var confirmedReading = mConfirmationModal.FindViewById<EditText>(Resource.Id.readingEditText).Text;
                    mReadings.Add(confirmedReading);
                    mConfirmationModal.Dismiss();
                    SendEmail();
                    UpdateReadingsText();
                };
            }
            view.FindViewById<EditText>(Resource.Id.readingEditText).Text = reading;
            using(var stream = image.ToMemoryStream())
            {
                view.FindViewById<ImageView>(Resource.Id.readingImage).SetImageBitmap(BitmapFactory.DecodeStream(stream));
            }
            alert.SetView(view);
            mConfirmationModal = alert.Create();
            mConfirmationModal.Show();
        }

        private void ShowEmailTemplateModal()
        {
            var alert = new AlertDialog.Builder(Context);
            var view = mLayoutInflater.Inflate(Resource.Layout.email_template, null);
            view.FindViewById<Button>(Resource.Id.cancelEmailTemplateButton).Click += (object sender, EventArgs e) =>
            {
                Toast.MakeText(Context, "Katkestatud", ToastLength.Short).Show();
                mEmailTemplateModal.Dismiss();
            };
            view.FindViewById<Button>(Resource.Id.saveEmailTemplateButton).Click += (object sender, EventArgs e) =>
            {
                Toast.MakeText(Context, "E-kirja mall salvestatud", ToastLength.Short).Show();
                mEmailSettings.MessageTemplate = mEmailTemplateModal.FindViewById<EditText>(Resource.Id.emailTemplateInput).Text;
                mEmailSettings.Subject = mEmailTemplateModal.FindViewById<EditText>(Resource.Id.emailSubjectInput).Text;
                mEmailSettings.Recipient = mEmailTemplateModal.FindViewById<EditText>(Resource.Id.emailRecipientInput).Text;
                SaveSettings(mEmailSettings);
                mEmailTemplateModal.Dismiss();
            };
            view.FindViewById<EditText>(Resource.Id.emailTemplateInput).Text = mEmailSettings.MessageTemplate;
            view.FindViewById<EditText>(Resource.Id.emailSubjectInput).Text = mEmailSettings.Subject;
            view.FindViewById<EditText>(Resource.Id.emailRecipientInput).Text = mEmailSettings.Recipient;
            alert.SetView(view);
            mEmailTemplateModal = alert.Create();
            mEmailTemplateModal.Show();
        }

        private void SendEmail()
        {
            if(mEmailSettings.NumberOfRequiredReadings > mReadings.Count)
            {
                Toast.MakeText(Context, $"E-kirja koostamiseks on vaja {mEmailSettings.NumberOfRequiredReadings } näitu!", ToastLength.Short).Show();
                return;
            }
            var emailMessenger = CrossMessaging.Current.EmailMessenger;
            if (emailMessenger.CanSendEmail)
            {
                var email = new EmailMessageBuilder()
                  .To(mEmailSettings.Recipient)
                  .Subject(mEmailSettings.Subject)
                  .Body(mEmailSettings.GetMessage(mReadings.ToArray()))
                  .Build();
                emailMessenger.SendEmail(email);
            }
        }

        private void RefreshSectorColorButtonResource()
        {
            this.mSectorColorToggleButton.SetImageResource(this.mSectorsAreDark ? Resource.Drawable.dark_sector_button : Resource.Drawable.light_sector_button);
        }

        private EmailSettings LoadSettings()
        {
            var settings = new EmailSettings();
            var prefs = Context.GetSharedPreferences(EMAIL_TEMPLATE_NAME, FileCreationMode.Private);
            settings.MessageTemplate = prefs.GetString("email_template", settings.MessageTemplate);
            settings.Recipient = prefs.GetString("recipient", settings.Recipient);
            settings.Subject = prefs.GetString("subject", settings.Subject);
            return settings;
        }

        private void SaveSettings(EmailSettings settings)
        {
            var prefs = Context.GetSharedPreferences(EMAIL_TEMPLATE_NAME, FileCreationMode.Private).Edit();
            prefs.PutString("email_template", settings.MessageTemplate);
            prefs.PutString("recipient", settings.Recipient);
            prefs.PutString("subject", settings.Subject);
            prefs.Commit();
        }

        private void UpdateReadingsText()
        {
            mTextView.Text = string.Join("\n", mReadings.Select((x, i) => $"{i}. {x}"));
        }
    }
}