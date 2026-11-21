import { HardDriveIcon, LockIcon, RefreshCcwIcon } from 'lucide-react';
import type React from 'react';
import { useTranslate } from '../../translate/i18n';
import { UISettingsRow } from '../../ui/settings/section/ui-settings-row';
import { UISettingsSection } from '../../ui/settings/section/ui-settings-section';

/**
 * How cloud backups work.
 */
export const SettingsCloudInfo: React.FC = () => {
    const { t } = useTranslate();

    return <UISettingsSection>
        <UISettingsRow icon={<RefreshCcwIcon />}
            label={t('cloud.info.when.label')}
            description={t('cloud.info.when.description')}
        />

        <UISettingsRow icon={<LockIcon />}
            label={t('cloud.info.private.label')}
            description={t('cloud.info.private.description')}
        />

        <UISettingsRow icon={<HardDriveIcon />}
            label={t('cloud.info.local.label')}
            description={t('cloud.info.local.description')}
        />
    </UISettingsSection>;
};
