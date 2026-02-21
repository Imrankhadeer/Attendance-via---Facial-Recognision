# Facial Recognition Attendance System

A professional desktop application built with **C# (.NET 8 WPF)**, **PostgreSQL**, and **OpenCV/ArcFace** for real-time student attendance tracking.

## 🚀 Features
- **Student Registration**: Capture face from webcam and store embeddings.
- **Real-time Attendance**: Detect and recognize faces to mark attendance automatically.
- **Duplicate Prevention**: Ensures a student is marked present only once per day.
- **Dashboard**: View daily statistics and recent activity.
- **Architecture**: Clean MVVM architecture with Service and Repository layers.

## 🛠 Prerequisites
1. **.NET 8 SDK**: Installed.
2. **PostgreSQL**: Installed.
3. **Webcam**: Required for face capture.

## ⚙️ Setup Instructions

### 1. Database Setup
1. Create a PostgreSQL database named `face_attendance`.
2. Run the schema script located at `FaceAttendance.Data/Scripts/schema.sql` to create the tables.
3. Update the connection string in `FaceAttendance.UI/appsettings.json` if your credentials differ:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Port=5432;Database=face_attendance;Username=postgres;Password=yourpassword"
   }
   ```

### 2. Model Files
The **InsightFace Buffalo_l** model files have already been downloaded and placed in the `FaceAttendance.UI/buffalo_l` folder.
- They are already configured to copy to the Output Directory on build.

### 3. Running the Application
**Using Visual Studio:**
1. Open `FaceAttendanceSystem.sln` (or open the folder).
2. Set `FaceAttendance.UI` as the Startup Project.
3. Build and Run (F5).

**Using CLI:**
```powershell
dotnet restore
cd FaceAttendance.UI
dotnet run
```

## 🏗 Project Structure
- **FaceAttendance.Core**: Domain models (`Student`, `AttendanceRecord`) and Interfaces.
- **FaceAttendance.Data**: PostgreSQL repository implementation (`AttendanceRepository`).
- **FaceAttendance.Services**: 
  - `CameraService` (Emgu.CV)
  - `FaceRecognitionService` (OnnxRuntime)
  - `AttendanceService` (Business Logic)
- **FaceAttendance.UI**: WPF Views, ViewModels, and Dependency Injection setup.

## 📝 Notes
- The first run might take a moment to initialize the camera and load the ONNX model.
- Ensure your webcam is not being used by another application.
