import { ExternalLinkIcon, FolderTreeIcon, ShieldCheckIcon } from 'lucide-react';
import type React from "react";
import { useTranslate } from '../../translate/i18n';
import { UISettingsRow } from '../../ui/settings/section/ui-settings-row';
import { UISettingsSection } from '../../ui/settings/section/ui-settings-section';

export const SettingsExternalLeft: React.FC = () => {
    const { t } = useTranslate();

    return <UISettingsSection>
        <UISettingsRow icon={<ExternalLinkIcon />}
            label={t('settings.external.1.label')}
            description={t('settings.external.1.description')}
        />

        <UISettingsRow icon={<FolderTreeIcon />}
            label={t('settings.external.2.label')}
            description={t('settings.external.2.description')}
        />

        <UISettingsRow icon={<ShieldCheckIcon />}
            label={t('settings.external.3.label')}
            description={t('settings.external.3.description')}
        />
    </UISettingsSection>;
};
