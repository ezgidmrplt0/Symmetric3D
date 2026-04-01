#import <UIKit/UIKit.h>

extern "C" {
    void _iOS_VibrateLight() {
        if (@available(iOS 10.0, *)) {
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [generator prepare];
            [generator impactOccurred];
        } else {
            AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
        }
    }

    void _iOS_VibrateMedium() {
        if (@available(iOS 10.0, *)) {
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
            [generator prepare];
            [generator impactOccurred];
        } else {
            AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
        }
    }

    void _iOS_VibrateHeavy() {
        if (@available(iOS 10.0, *)) {
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
            [generator prepare];
            [generator impactOccurred];
        } else {
            AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
        }
    }
}
