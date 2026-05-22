DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'typeid') THEN
            CREATE DOMAIN public.typeid AS text;
        END IF;
    END
$$;

COMMENT ON DOMAIN public.typeid IS 'A custom type representing unique identifiers as text.';
