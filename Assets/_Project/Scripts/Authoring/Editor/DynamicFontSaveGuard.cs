using UnityEditor;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;

namespace MergeBullet.Editor
{
    /// <summary>
    /// UI 폰트 에셋은 동적(Dynamic) 모드라 화면에 그린 글자를 그때그때 아틀라스에 굽는데,
    /// 구운 글자가 에셋 파일에 같이 저장되면 Play 한 번에 파일이 수 MB씩 커지고 git에 변경으로 잡힌다.
    /// 그래서 폰트 에셋이 디스크에 저장되기 직전에 구운 글자를 비운다 (Play 종료 뒤든 언제든 저장 경로는 하나라서 여기서 막는다).
    /// 비워도 다음에 화면에 그릴 때 다시 구워지므로 게임에는 영향이 없다.
    /// </summary>
    sealed class DynamicFontSaveGuard : AssetModificationProcessor
    {
        const string FontAssetFolder = "Assets/_Project/Fonts/Assets/";

        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                if (!path.StartsWith(FontAssetFolder) || !path.EndsWith(".asset")) continue;

                FontAsset font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;

                // true: 아틀라스 텍스처 크기도 0으로 (새로 만든 직후와 같은 상태)
                font.ClearFontAssetData(true);
            }
            return paths;
        }
    }
}
