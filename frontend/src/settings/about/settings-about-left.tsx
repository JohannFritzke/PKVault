import { Anchor, Avatar, Group, Stack, Text } from '@mantine/core';
import { CircleUserRoundIcon, CodeXmlIcon, CopyrightIcon, MessagesSquareIcon } from 'lucide-react';
import type React from "react";
import { useTranslate } from '../../translate/i18n';
import { UILinkWithIcon } from '../../ui/link-with-icon/ui-link-with-icon';
import { UISettingsRow } from '../../ui/settings/section/ui-settings-row';
import { UISettingsSection } from '../../ui/settings/section/ui-settings-section';

export const SettingsAboutLeft: React.FC = () => {
    const { t } = useTranslate();

    return <>
        <UISettingsSection>
            <UISettingsRow icon={<CodeXmlIcon />} label={t('settings.about.1.label')}>
                <UILinkWithIcon href='https://github.com/Chnapy/PKVault' target='__blank'>
                    {t('settings.about.1.description')}
                </UILinkWithIcon>
            </UISettingsRow>

            <UISettingsRow icon={<CircleUserRoundIcon />} label={t('settings.about.2.label')}>
                <Anchor href='https://github.com/Chnapy' target='__blank'>
                    <Group component='span' gap='sm'>
                        <Avatar src='https://avatars.githubusercontent.com/u/7474483?v=4' size='sm' />
                        Chnapy
                    </Group>
                </Anchor>
            </UISettingsRow>

            <UISettingsRow icon={<CopyrightIcon />} label={t('settings.about.3.label')}>
                <Group gap='xs' justify='flex-end'>
                    <UILinkWithIcon href='https://github.com/Chnapy/PKVault/blob/main/LICENSE' target='__blank'>
                        GPLv3
                    </UILinkWithIcon>
                    -
                    <UILinkWithIcon href='https://github.com/Chnapy/PKVault/blob/main/README.md#licenses' target='__blank'>
                        {t('settings.about.3.description')}
                    </UILinkWithIcon>
                </Group>
            </UISettingsRow>

            <UISettingsRow icon={<MessagesSquareIcon />} label={t('settings.about.4.label')}>
                <Stack gap='xs' align='flex-end'>
                    <UILinkWithIcon href='https://github.com/Chnapy/PKVault/issues' target='__blank'>
                        {t('settings.about.4.description.1')}
                    </UILinkWithIcon>
                    <UILinkWithIcon href='https://projectpokemon.org/home/forums/topic/67239-pkvault-centralized-pkm-storage-management-pokedex-app' target='__blank'>
                        {t('settings.about.4.description.2')}
                    </UILinkWithIcon>
                </Stack>
            </UISettingsRow>
        </UISettingsSection>

        <UISettingsSection padded>
            <Text c='dimmed' fz='sm'>
                {t('settings.about.5.description.1')}
                <br />{t('settings.about.5.description.2')}
                <br />{t('settings.about.5.description.3')}
            </Text>
        </UISettingsSection>
    </>;
};
