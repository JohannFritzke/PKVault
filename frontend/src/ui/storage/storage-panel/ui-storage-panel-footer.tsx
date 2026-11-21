import { Box, Group, NumberFormatter } from '@mantine/core';
import type React from 'react';
import { useTranslate } from '../../../translate/i18n';

export type UIStoragePanelFooterProps = {
    boxSize: number;
    pkmCount: number;
    pkmTotalCount: number;
};

export const UIStoragePanelFooter: React.FC<UIStoragePanelFooterProps> = ({ boxSize, pkmCount, pkmTotalCount }) => {
    const { t } = useTranslate();

    return <Group justify='space-between' wrap='nowrap' gap='md'>
        <Box>
            <NumberFormatter value={pkmCount} /> / <NumberFormatter value={boxSize} />
        </Box>
        <Box ta='right'>
            {t('total')} <NumberFormatter value={pkmTotalCount} />
        </Box>
    </Group>;
};
