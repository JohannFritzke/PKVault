import { FileIcon, FolderIcon } from 'lucide-react';
import type React from "react";
import { RuntimeSystem } from '../../data/sdk/model';
import { useSettingsGet } from '../../data/sdk/settings/settings.gen';
import { useTranslate } from '../../translate/i18n';
import { UIPathButton } from '../../ui/form/globs-input/ui-path-button';
import { UIBallIcon } from '../../ui/icon/ui-ball-icon';
import { UIPathLine } from '../../ui/path/ui-path-line';
import { UIPopover } from '../../ui/popover/ui-popover';
import { UISettingsRow } from '../../ui/settings/section/ui-settings-row';
import { UISettingsSection } from '../../ui/settings/section/ui-settings-section';
import classes from '../../ui/settings/ui-settings.module.css';
import { switchUtil } from '../../util/switch-util';
import { SettingsAndroidDirectory } from './settings-android-directory';

export const SettingsMainSystem: React.FC = () => {
    const { t } = useTranslate();

    const settingsQuery = useSettingsGet();
    const settings = settingsQuery.data?.data;

    const appDirectory = settings?.appDirectory ?? '-';

    return <UISettingsSection>
        <UISettingsRow icon={<UIBallIcon />} label='PKVault'>
            <span>v{settings?.version} - {switchUtil(settings?.runtimeSystem ?? 0, {
                [ RuntimeSystem.UNKNOWN ]: t('settings.system.unknown'),
                [ RuntimeSystem.DOCKER ]: 'Docker',
                [ RuntimeSystem.WINDOWS ]: t('settings.system.windows'),
                [ RuntimeSystem.LINUX ]: t('settings.system.linux'),
                [ RuntimeSystem.STEAMDECK ]: t('settings.system.steamdeck'),
                [ RuntimeSystem.MACOS ]: t('settings.system.macos'),
                [ RuntimeSystem.ANDROID ]: t('settings.system.android'),
            })}</span>
        </UISettingsRow>

        <UISettingsRow icon={<img src="https://projectpokemon.org/favicon.ico" />} label='PKHeX'>
            <span>{settings?.pkhexVersion}</span>
        </UISettingsRow>

        <UISettingsRow icon={<FolderIcon />} label={t('settings.relative-paths')}>
            {settings?.runtimeSystem === RuntimeSystem.ANDROID
                ? <UIPopover
                    dropdown={<SettingsAndroidDirectory />}
                >
                    <UIPathButton
                        value={appDirectory}
                        pkvaultPath={appDirectory}
                        uploadPath={settings.savesUploadsPath}
                        icons={null}
                    />
                </UIPopover>
                : <UIPathLine className={classes.mono}>{appDirectory}</UIPathLine>}
        </UISettingsRow>

        <UISettingsRow icon={<FileIcon />} label={t('settings.form.config')}>
            <UIPathLine className={classes.mono}>{settings?.settingsPath ?? '-'}</UIPathLine>
        </UISettingsRow>
    </UISettingsSection>;
};
