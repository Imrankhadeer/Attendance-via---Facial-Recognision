# Facial Recognition Attendance System

A professional desktop application built with **C# (.NET 8 WPF)**, **PostgreSQL**, and **OpenCV/ArcFace** for real-time student attendance tracking.

## 🚀 Features
- **Student Registration**: Capture face from webcam and assign Course, Year, Semester and Group mappings.
- **Real-time Attendance**: Detect and recognize faces to mark attendance automatically, conditionally gated by Active Sessions.
- **Duplicate Prevention**: Ensures a student is marked present only once per session/day.
- **WPF Dashboard**: View daily statistics and recent activity directly on the local capture machine.
- **Web Portal Ecosystem**: Multi-Role web dashboards managing Faculty accounts, Active Sessions, Student logs, and Administration.
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

### 3. Running the Applications

**Desktop Application (Face Recognition)**
```powershell
cd FaceAttendance.UI
dotnet run
```

**Admin Panel Web Application**
```powershell
cd FaceAttendance.AdminWeb
dotnet run
```
* The Web Portal will then be available at: `http://localhost:5177`

### 4. Navigating The Web Portal Roles

The Web Portal uses a single login page (`http://localhost:5177/Account/Login`), but dynamically shifts capabilities based on your Role selection:

**A. Administrator**
- **Login As**: Administrator
- **Default Username**: `admin`
- **Default Password**: `admin123`
- **Capabilities**: Can manage registered student profiles (update Course/Year mappings or delete faces), view all universal attendance logs, and **Create/Delete Faculty Accounts**.

**B. Faculty**
- **Login As**: Faculty
- **Username**: Created by Admin
- **Password**: Created by Admin
- **Capabilities**: Arrives at the Faculty Dashboard. From here, they can input a Course/Year/Semester/Group configuration and click **"Open Session"**. 
  - *Note: The WPF Camera Application will REFUSE to mark attendance for a student until a Faculty member has an Active Session open for that student's specific grouping.*

**C. Student**
- **Login As**: Student (Check Attendance)
- **Username**: Their literal `Student ID` (e.g., 1001)
- **Password**: Their exact `Full Name` mapping (e.g., John Doe)
- **Capabilities**: Can view a read-only table of exactly when and where their own attendance was marked.

### 5. Changing Admin Password
To change the admin password, run the following SQL command in your PostgreSQL database:
```sql
UPDATE admins SET password_hash = 'YourNewPasswordHere' WHERE username = 'admin';
```

## 🏗 Project Structure
- **FaceAttendance.Core**: Domain models (`Student`, `AttendanceRecord`) and Interfaces.
- **FaceAttendance.Data**: PostgreSQL repository implementation (`AttendanceRepository`).
- **FaceAttendance.Services**: 
  - `CameraService` (Emgu.CV)
  - `FaceRecognitionService` (OnnxRuntime)
  - `AttendanceService` (Business Logic)
- **FaceAttendance.UI**: WPF Views, ViewModels, and Dependency Injection setup.
- **FaceAttendance.AdminWeb**: ASP.NET Core MVC App for managing attendance logs and registered students.

## 📝 Notes
- The first run might take a moment to initialize the camera and load the ONNX model.
- Ensure your webcam is not being used by another application.
