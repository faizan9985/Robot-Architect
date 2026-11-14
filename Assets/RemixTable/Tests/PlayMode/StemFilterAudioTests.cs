using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class StemFilterAudioTests
    {
        [UnityTest] public IEnumerator EffectAutomationDrivesOnlyThatStemsFilters()
        {
            var root=new GameObject("filter audio");root.SetActive(false);
            var manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();
            manager.library=root.AddComponent<SongLibraryManager>();
            var audio=root.AddComponent<RemixAudioManager>();audio.manager=manager;manager.audioManager=audio;
            var clip=AudioClip.Create("filter tone",96000,1,48000,false);
            var data=new float[96000];for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(i*2*Mathf.PI*440/48000)*.2f;
            clip.SetData(data,0);
            var song=new SongMetadata{id="filter",stems=new StemMetadata[4]};
            audio.sources=new AudioSource[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("stem "+i);go.transform.SetParent(root.transform);
                audio.sources[i]=go.AddComponent<AudioSource>();song.stems[i]=new StemMetadata{id=(StemId)i,clip=clip};
            }
            manager.library.songs=new[]{song};
            if(Object.FindFirstObjectByType<AudioListener>()==null)root.AddComponent<AudioListener>();
            root.SetActive(true);audio.Load(song);
            yield return new WaitForSeconds(.4f);
            for(int i=0;i<4;i++)
            {
                Assert.AreEqual(StemFilter.MaxCutoff,audio.Lowpass((StemId)i).cutoffFrequency,1f,"neutral low-pass");
                Assert.AreEqual(StemFilter.MinHighpassCutoff,audio.Highpass((StemId)i).cutoffFrequency,1f,"neutral high-pass");
                Assert.AreSame(audio.sources[i].gameObject,audio.Lowpass((StemId)i).gameObject,"filter sits on its own stem");
            }
            manager.automation.Upsert(new AutomationEdit{songId="filter",stem=StemId.Bass,parameter=ParameterType.Effect,wholeStem=true,value=-1});
            manager.automation.Upsert(new AutomationEdit{songId="filter",stem=StemId.Vocals,parameter=ParameterType.Effect,wholeStem=true,value=1});
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(-1,audio.Effect(StemId.Bass),.01f);
            Assert.AreEqual(1,audio.Effect(StemId.Vocals),.01f);
            Assert.AreEqual(StemFilter.MinLowpassCutoff,audio.Lowpass(StemId.Bass).cutoffFrequency,15f);
            Assert.AreEqual(StemFilter.MaxHighpassCutoff,audio.Highpass(StemId.Vocals).cutoffFrequency,30f);
            Assert.AreEqual(StemFilter.MaxCutoff,audio.Lowpass(StemId.Drums).cutoffFrequency,1f,"untouched stem stays neutral");
            Assert.AreEqual(StemFilter.MinHighpassCutoff,audio.Highpass(StemId.Melody).cutoffFrequency,1f,"untouched stem stays neutral");
            Assert.IsTrue(manager.clock.playing);
            foreach(var s in audio.sources)Assert.That(System.Math.Abs(s.timeSamples-audio.sources[0].timeSamples),Is.LessThan(1024),"stems stay in sync");
            // Clearing the edit returns smoothly to neutral.
            manager.automation.ResetStem("filter",StemId.Bass);
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(StemFilter.MaxCutoff,audio.Lowpass(StemId.Bass).cutoffFrequency,50f);
            Object.Destroy(root);Object.Destroy(clip);Object.Destroy(manager.design);yield return null;
        }
    }
}
