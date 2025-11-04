# Entfernt Explorer-Kontextmenü-Einträge für GDPR Blur Pro
@(
  'HKCU:\Software\Classes\SystemFileAssociations\image\shell\GDPRBlurPro',
  'HKCU:\Software\Classes\SystemFileAssociations\video\shell\GDPRBlurPro',
  'HKCU:\Software\Classes\SystemFileAssociations\.mp4\shell\GDPRBlurPro',
  'HKCU:\Software\Classes\SystemFileAssociations\.mov\shell\GDPRBlurPro',
  'HKCU:\Software\Classes\SystemFileAssociations\.avi\shell\GDPRBlurPro',
  'HKCU:\Software\Classes\SystemFileAssociations\.mkv\shell\GDPRBlurPro',
  'HKCU:\Software\Classes\SystemFileAssociations\.wmv\shell\GDPRBlurPro',
  'HKCU:\Software\Classes\Directory\shell\GDPRBlurPro'
) | ForEach-Object { Remove-Item -Path $_ -Recurse -Force -ErrorAction SilentlyContinue }
taskkill /f /im explorer.exe 2>$null | Out-Null
Start-Process explorer.exe
