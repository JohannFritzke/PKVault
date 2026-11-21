import { Gamepad2Icon, GlobeIcon, PenOffIcon, ShieldOffIcon } from 'lucide-react';
import React from "react";
import { useFormContext, useWatch } from 'react-hook-form';
import { useSettingsGet } from '../../data/sdk/settings/settings.gen';
import type { SettingsFormData } from '../../pages/settings';
import { languages, useTranslate } from '../../translate/i18n';
import { UISelect } from '../../ui/form/select/ui-select';
import { UISwitch } from '../../ui/form/switch/ui-switch';
import { useControlsContext } from '../../ui/interaction/controls/provider/use-controls-context';
import { UISettingsRow } from '../../ui/settings/section/ui-settings-row';
import { UISettingsSection } from '../../ui/settings/section/ui-settings-section';

export const SettingsMainLeft: React.FC = () => {
    const { t } = useTranslate();

    const settingsQuery = useSettingsGet();
    const settings = settingsQuery.data?.data;

    const form = useFormContext<SettingsFormData>();

    const [ language ] = useWatch({ control: form.control, name: [ 'language' ] });

    const { enableGamepad, toggleGamepad } = useControlsContext();

    return <UISettingsSection>
        <UISettingsRow icon={<GlobeIcon />} forInput='language' label={t('settings.form.language')}>
            <UISelect
                name='language'
                controlLabel={t('settings.form.language')}
                value={language}
                data={Object.entries(languages)
                    .map(([ value, label ]) => ({ value, label }))}
                onChange={value => value && form.setValue('language', value, { shouldDirty: true })}
                disabled={!settings?.canUpdateSettings}
                scrollAreaProps={{ type: 'always' }}
                w={220}
            />
        </UISettingsRow>

        <UISettingsRow icon={<PenOffIcon />} forInput='hidE_CHEATS'
            label={t('settings.form.hide-cheats')}
            description={t('settings.form.hide-cheats.description')}
        >
            <UISwitch
                {...form.register('hidE_CHEATS')}
                defaultChecked={form.getValues('hidE_CHEATS')}
                controlLabel={t('settings.form.hide-cheats')}
            />
        </UISettingsRow>

        <UISettingsRow icon={<ShieldOffIcon />} forInput='skiP_LEGALITY_CHECKS'
            label={t('settings.form.skip-legality')}
            description={t('settings.form.skip-legality.description')}
        >
            <UISwitch
                {...form.register('skiP_LEGALITY_CHECKS')}
                defaultChecked={form.getValues('skiP_LEGALITY_CHECKS')}
                controlLabel={t('settings.form.skip-legality')}
            />
        </UISettingsRow>

        <UISettingsRow icon={<Gamepad2Icon />} forInput='enable-gamepad'
            label={t('settings.form.enable-gamepad')}
            description={t('settings.form.enable-gamepad.description')}
        >
            <UISwitch
                name='enable-gamepad'
                checked={enableGamepad}
                onChange={toggleGamepad}
                controlLabel={t('settings.form.enable-gamepad')}
            />
        </UISettingsRow>
    </UISettingsSection>;
};
