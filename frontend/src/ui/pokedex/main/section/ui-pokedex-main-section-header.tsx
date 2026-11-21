import { Badge, Divider, Group, Text, ThemeIcon } from '@mantine/core';
import { ListIcon } from 'lucide-react';
import type React from 'react';
import { UIPokedexIcons } from '../../icons/ui-pokedex-icons';

type UIPokedexMainSectionHeaderProps = {
    generation: string;
    regions: string[];
    games: React.ReactNode;
    seenCount: number;
    caughtCount: number;
    ownedCount: number;
    shinyCount: number;
    totalCount: number;
};

export const UIPokedexMainSectionHeader: React.FC<UIPokedexMainSectionHeaderProps> = ({
    generation, regions, games, seenCount, caughtCount, ownedCount, shinyCount, totalCount
}) => {
    const renderCount = (icon: React.ReactNode, count: number) => <Group component='span' gap={2} wrap='nowrap'>
        {icon}
        <Text component='span' size='sm' fw={500} c='dimmed' style={{ fontVariantNumeric: 'tabular-nums' }}>
            {count}
        </Text>
    </Group>;

    return <Group justify='space-between' py='sm' wrap='nowrap' style={{ overflow: 'hidden' }}>
        <Group gap='sm' wrap='nowrap' style={{ whiteSpace: 'nowrap', overflow: 'hidden' }}>
            <Text component='span' fw={600}>{generation}</Text>
            {regions.map(region => <Badge key={region} variant='light' color='gray' visibleFrom='sm'>{region}</Badge>)}
            <Divider orientation='vertical' />
            {games}
        </Group>

        <Group gap='md' pr='md' wrap='nowrap'>
            {renderCount(<UIPokedexIcons.Seen size='sm' />, seenCount)}
            {renderCount(<UIPokedexIcons.Caught size='sm' />, caughtCount)}
            {renderCount(<UIPokedexIcons.Owned size='sm' />, ownedCount)}
            {renderCount(<UIPokedexIcons.Shiny size='sm' />, shinyCount)}
            <Divider orientation='vertical' />
            {renderCount(<ThemeIcon variant='transparent' size='sm' c='dimmed'>
                <ListIcon />
            </ThemeIcon>, totalCount)}
        </Group>
    </Group>;
};
