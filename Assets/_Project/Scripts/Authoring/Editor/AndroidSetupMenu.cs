using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MergeBullet.Editor
{
    /// <summary>
    /// 안드로이드 Player Settings를 코드로 맞춘다. 설정 창에서 손으로 바꾸면 무엇을 왜 바꿨는지 남지 않아서,
    /// 값과 이유를 여기 모아 둔다. 다시 실행해도 같은 값이 된다.
    /// </summary>
    static class AndroidSetupMenu
    {
        // 구글 플레이에 한 번 올리면 바꿀 수 없다
        const string PackageName = "com.sinaki.mergebullet";
        const string CompanyName = "sinaki";

        [MenuItem("MergeBullet/Setup Android Player Settings")]
        static void Setup()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);

            // 가로 화면 전용 (좌우로만 돌아감). 맵·UI가 16:9 가로 기준이라 세로는 지원하지 않는다
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 구글 플레이는 64비트(ARM64)가 필수이고, ARM64는 IL2CPP로만 빌드된다
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // 안드로이드 8.0 이상 (Unity 6 최소 지원과 같음)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            AssetDatabase.SaveAssets();
            Debug.Log($"안드로이드 설정 완료: {PackageName}, 가로 화면, IL2CPP ARM64.");
        }
    }
}
