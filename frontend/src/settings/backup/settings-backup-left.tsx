import { FileArchiveIcon, FolderTreeIcon, RefreshCcwIcon } from 'lucide-react';
import type React from "react";
import { useTranslate } from '../../translate/i18n';
import { UISettingsRow } from '../../ui/settings/section/ui-settings-row';
import { UISettingsSection } from '../../ui/settings/section/ui-settings-section';

/**
 * How backups work.
 */
export const SettingsBackupLeft: React.FC = () => {
    const { t } = useTranslate();

    return <UISettingsSection>
        <UISettingsRow icon={<RefreshCcwIcon />}
            label={t('settings.backups.2.label')}
            description={t('settings.backups.2.description')}
        />

        <UISettingsRow icon={<FolderTreeIcon />}
            label={t('settings.backups.3.label')}
            description={t('settings.backups.3.description')}
        />

        <UISettingsRow icon={<FileArchiveIcon />}
            label={t('settings.backups.4.label')}
            description={t('settings.backups.4.description')}
        />
    </UISettingsSection>;
};
