<p align="center">
<img alt=".NET" src="https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
<img alt="Windows" src="https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" />
<img alt="Linux" src="https://img.shields.io/badge/Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black" />
<img alt="License" src="https://img.shields.io/badge/GPL--3.0-red?style=for-the-badge" />
</p>

# 💾 CloudZBackup

**CloudZBackup is a console tool to securely and efficiently synchronize and maintain backups between folders.**

## 📋 What CloudZBackup does

CloudZBackup lets you keep a destination folder up to date with a source folder:

* **🔄 Full synchronization** - Keep destination identical to source
* **➕ Add mode** - Copy only what’s missing in the destination
* **➖ Remove mode** - Delete extra files in the destination
* **⚡ Real-time progress** - Clear, detailed progress bar
* **🧠 Change detection** - Compare and plan operations before executing

## ❓ Why use CloudZBackup?

* **🖥️ Professional console UI** - Clean visual output with sections and status
* **📦 Simple and straightforward** - A single command with clear parameters
* **💾 IO-optimized** - Concurrency settings to avoid saturating removable drives
* **🧰 No external dependencies** - Just .NET
* **🆓 Open source** - Free and extensible

## ⭐ Key features

* **🔀 sync/add/remove modes**
* **📂 Automatic folder creation and deletion**
* **📄 Copy and overwrite files when needed**
* **📊 Final summary with detailed stats**
* **🛑 Clean cancellation with Ctrl+C**

## 📘 How to use CloudZBackup

You can use parameters or let the app prompt you interactively:

**Example with parameters:**

* `--source` Source path
* `--dest` Destination path
* `--mode` Mode (`sync`, `add`, `remove`)

Example:
`dotnet CloudZBackup.Terminal.dll --source "C:\Data" --dest "E:\Backup" --mode sync`

**Interactive mode:**
If you don’t pass parameters, the application will ask for the values in the console.

## 🚀 Roadmap

CloudZBackup is constantly evolving. Here's what we're planning for future releases:

* **⚙️ Advanced flags for filters and exclusions**

We're committed to continuously improving CloudZCrypt based on user feedback and security best practices. Your suggestions are always welcome and will help shape the application's future.

## 📸 Screenshots

<img width="1097" height="554" alt="image" src="https://github.com/user-attachments/assets/1ed126e7-9a3f-4526-bdef-0ffba90f82ce" />

## 💡How to Contribute

- We welcome contributions from everyone, regardless of your technical background!
- Every contribution matters and helps make this project better for everyone!

#### For Non-Developers
You can make valuable contributions too:
- **Report Bugs**: Found something that doesn't work? Let us know by opening an issue.
- **Suggest Features**: Have ideas for new features or improvements? We'd love to hear them.
- **Translations**: Help translate the application into your language.
- **Documentation**: Improve or clarify our documentation.
- **Spread the Word**: Share the project on social media, blog about it, or tell your friends.
- **User Testing**: Try new features and provide feedback.

#### For Developers

1. Fork the repository
2. Create a feature branch from `develop`
3. Implement your changes with documentation and tests
4. Submit a pull request

We especially welcome contributions for UI and performance improvements.
